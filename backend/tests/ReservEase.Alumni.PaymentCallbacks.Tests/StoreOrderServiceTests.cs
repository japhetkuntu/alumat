using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PaymentCallbacks.Tests;

public class StoreOrderServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IPaystackService> Paystack { get; } = new();
        public Mock<IStorageService> Storage { get; } = new();
        public Mock<ITemporalClientProvider> Temporal { get; } = new();
        public PaystackConfig PaystackConfig { get; } = new() { GatewayFeePercentage = 1.95m, GatewayFeeSafetyBufferSubunit = 2 };
        public List<InitializePaymentRequest> Initialized { get; } = new();
        public AuthData Member { get; } = new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };
        public StoreOrderService Service { get; private set; }
        private readonly string callbackUrl;

        public Rig(Action<Institution>? institution = null, bool paystackAccepts = true, string callbackUrl = "https://app.test/pay")
        {
            Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()))
                .Callback<InitializePaymentRequest>(r => Initialized.Add(r))
                .ReturnsAsync(() => new InitializePaymentResponse
                {
                    Status = paystackAccepts,
                    Message = paystackAccepts ? "ok" : "Invalid key",
                    Data = new InitializePaymentData { AuthorizationUrl = "https://paystack/pay" },
                });
            Storage.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("https://cdn/file.pdf");
            Temporal.SetupGet(t => t.IsAvailable).Returns(false);

            var inst = new Institution { Id = Tenant, Slug = "umat", Name = "UMaT" };
            institution?.Invoke(inst);
            using (var seed = TestDb.Create(DbName, Tenant)) { seed.Institutions.Add(inst); seed.SaveChanges(); }

            this.callbackUrl = callbackUrl;
            Service = Fresh();
        }

        /// <summary>A new service over a new context, like the next HTTP request would get — avoids reading entities this context already tracks.</summary>
        public StoreOrderService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant, TestDb.Tenant(Tenant, "umat"));
            return Service = new StoreOrderService(
                new AlumniPgRepository<StoreOrder>(db), new AlumniPgRepository<StoreProduct>(db), new AlumniPgRepository<StoreProductVariant>(db),
                new AlumniPgRepository<Institution>(db), new AlumniPgRepository<MemberEntity>(db), TestDb.Tenant(Tenant, "umat"),
                Paystack.Object, PaystackConfig, Temporal.Object, Storage.Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PaystackConfig:CallbackUrl"] = callbackUrl }).Build(),
                NullLogger<StoreOrderService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task AddProduct(StoreProduct product, params StoreProductVariant[] variants)
        {
            using var db = Db();
            product.InstitutionId = Tenant;
            db.StoreProducts.Add(product);
            foreach (var v in variants) { v.InstitutionId = Tenant; v.ProductId = product.Id; db.StoreProductVariants.Add(v); }
            await db.SaveChangesAsync();
        }

        public async Task<StoreOrder> OnlyOrder() => await Db().StoreOrders.SingleAsync();
    }

    private static CheckoutRequest Cart(params (string product, int qty, string? variant)[] lines) => new()
    {
        Items = lines.Select(l => new CheckoutItemRequest { ProductId = l.product, Quantity = l.qty, VariantId = l.variant }).ToList(),
    };

    private static StoreProduct Product(string id, decimal price, int stock = 10) => new() { Id = id, Name = $"Product {id}", Price = price, QuantityAvailable = stock };

    private static IFormFile File(string name = "proof.pdf", int length = 10) =>
        new FormFile(new MemoryStream(new byte[length]), 0, length, "f", name);

    // ── Validation of the cart ──────────────────────────────────────────

    [Fact]
    public async Task An_empty_cart_is_rejected()
    {
        var rig = new Rig();
        var response = await rig.Service.InitiateCheckoutAsync(new CheckoutRequest(), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("empty", response.Message);
        rig.Paystack.Verify(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_quantity_is_rejected(int qty)
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10));
        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", qty, null)), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("Quantity", response.Message);
    }

    [Fact]
    public async Task An_unknown_product_is_rejected()
    {
        var rig = new Rig();
        var response = await rig.Service.InitiateCheckoutAsync(Cart(("ghost", 1, null)), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("no longer available", response.Message);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Archived")]
    public async Task A_product_that_is_not_active_cannot_be_bought(string status)
    {
        var rig = new Rig();
        var product = Product("p1", 10); product.Status = status;
        await rig.AddProduct(product);
        Assert.Equal(400, (await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member)).Code);
    }

    [Fact]
    public async Task One_bad_line_rejects_the_whole_cart_and_nothing_is_saved_or_charged()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10));
        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null), ("ghost", 1, null)), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Equal(0, await rig.Db().StoreOrders.CountAsync());
        rig.Paystack.Verify(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()), Times.Never);
    }

    // ── Simple products, no subaccount ──────────────────────────────────

    [Fact]
    public async Task A_simple_product_creates_a_pending_order_and_initialises_paystack_for_the_exact_total()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 25.50m));

        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 3, null)), rig.Member);

        Assert.Equal(200, response.Code);
        Assert.Equal("https://paystack/pay", response.Data!.AuthorizationUrl);
        var order = await rig.OnlyOrder();
        Assert.Equal("Pending", order.Status);
        Assert.Equal(76.50m, order.TotalAmount);
        Assert.Equal(response.Data.Reference, order.TransactionRef);
        Assert.StartsWith("SO_", order.TransactionRef);
        Assert.Equal(8, order.OrderNumber.Length);
        Assert.Equal("m1", order.MemberId);
        Assert.Equal(("Ama", "Mensah", "ama@x.com"), (order.Member!.FirstName, order.Member.LastName, order.Member.Email));
        Assert.Equal("m1", order.CreatedBy);
        Assert.Equal("Paystack", order.PaymentMethod);

        var item = Assert.Single(order.Items);
        Assert.Equal((25.50m, 3), (item.UnitPrice, item.Quantity));

        var init = Assert.Single(rig.Initialized);
        Assert.Equal(7650, init.Amount);                       // pesewas
        Assert.Equal(order.TransactionRef, init.Reference);
        Assert.Equal("ama@x.com", init.Email);
        Assert.Null(init.Subaccount);
        Assert.Null(init.TransactionCharge);
        Assert.Null(init.Bearer);
        Assert.Equal("true", init.Metadata!["storeOrder"]);
        Assert.Equal("m1", init.Metadata["memberId"]);
    }

    [Fact]
    public async Task Without_a_subaccount_no_fees_are_added()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 100));
        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        var order = await rig.OnlyOrder();
        Assert.Equal((0m, 0m, 0m, 100m), (order.PlatformFeeAmount, order.GatewayFeeAmount, order.TransactionChargeAmount, order.GrossChargeAmount));
    }

    [Fact]
    public async Task Several_lines_are_summed()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("a", 10));
        await rig.AddProduct(Product("b", 2.25m));
        await rig.Service.InitiateCheckoutAsync(Cart(("a", 2, null), ("b", 4, null)), rig.Member);

        var order = await rig.OnlyOrder();
        Assert.Equal(29m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(2900, rig.Initialized.Single().Amount);
    }

    [Fact]
    public async Task The_callback_url_comes_from_the_request_then_from_config_with_callback_appended()
    {
        var rig = new Rig(callbackUrl: "https://app.test/pay/");
        await rig.AddProduct(Product("p1", 10));

        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);
        Assert.Equal("https://app.test/pay/callback", rig.Initialized.Last().CallbackUrl);

        var withOwn = Cart(("p1", 1, null)); withOwn.CallbackUrl = "https://custom/cb";
        await rig.Service.InitiateCheckoutAsync(withOwn, rig.Member);
        Assert.Equal("https://custom/cb", rig.Initialized.Last().CallbackUrl);
    }

    [Fact]
    public async Task A_configured_callback_that_already_ends_in_callback_is_left_alone()
    {
        var rig = new Rig(callbackUrl: "https://app.test/CALLBACK");
        await rig.AddProduct(Product("p1", 10));
        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);
        Assert.Equal("https://app.test/CALLBACK", rig.Initialized.Single().CallbackUrl);
    }

    [Fact]
    public async Task Product_snapshot_includes_image_delivery_info_and_stages()
    {
        var rig = new Rig();
        var product = Product("p1", 10);
        product.ImageUrls = ["https://img/1", "https://img/2"]; product.DeliveryInfo = "Collect at office"; product.Stages = ["Received", "Ready"];
        await rig.AddProduct(product);

        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        var item = (await rig.OnlyOrder()).Items.Single();
        Assert.Equal(("https://img/1", "Collect at office", "Received"), (item.ProductImageUrl, item.DeliveryInfo, item.CurrentStage));
        Assert.Equal(new[] { "Received", "Ready" }, item.Stages);
    }

    // ── Zero-Deduction fees with a subaccount ───────────────────────────

    [Fact]
    public async Task With_a_subaccount_the_payer_covers_platform_and_gateway_fees_and_paystack_gets_the_split_fields()
    {
        var rig = new Rig(i => { i.PaystackSubaccountCode = "ACCT_1"; i.PlatformFeePercentage = 2m; });
        await rig.AddProduct(Product("p1", 100));

        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        var order = await rig.OnlyOrder();
        var init = rig.Initialized.Single();
        var expected = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 2m, 1.95m, 0, null, 2);

        Assert.Equal(expected.ChargeAmountSubunit, init.Amount);
        Assert.Equal(expected.TransactionChargeSubunit, init.TransactionCharge);
        Assert.Equal("account", init.Bearer);
        Assert.Equal("ACCT_1", init.Subaccount);

        Assert.Equal(100m, order.TotalAmount);                 // the institution still nets exactly the item total
        Assert.Equal(expected.PlatformFeeSubunit / 100m, order.PlatformFeeAmount);
        Assert.Equal(expected.GatewayFeeSubunit / 100m, order.GatewayFeeAmount);
        Assert.Equal(expected.TransactionChargeSubunit / 100m, order.TransactionChargeAmount);
        Assert.Equal(expected.ChargeAmountSubunit / 100m, order.GrossChargeAmount);
        Assert.Equal(order.TotalAmount + order.PlatformFeeAmount + order.GatewayFeeAmount, order.GrossChargeAmount);
    }

    [Fact]
    public async Task Tiered_flat_fee_replaces_the_percentage_above_the_threshold()
    {
        var rig = new Rig(i => { i.PaystackSubaccountCode = "ACCT_1"; i.PlatformFeePercentage = 5m; i.PlatformFeeFlatThreshold = 500m; i.PlatformFeeFlatAmount = 20m; });
        await rig.AddProduct(Product("p1", 1000));

        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        Assert.Equal(20m, (await rig.OnlyOrder()).PlatformFeeAmount);
    }

    [Fact]
    public async Task A_blank_subaccount_code_is_treated_as_no_subaccount()
    {
        var rig = new Rig(i => { i.PaystackSubaccountCode = ""; i.PlatformFeePercentage = 5m; });
        await rig.AddProduct(Product("p1", 100));
        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        Assert.Equal(10000, rig.Initialized.Single().Amount);
        Assert.Null(rig.Initialized.Single().TransactionCharge);
    }

    // ── Stock ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Asking_for_more_than_is_in_stock_is_rejected()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10, stock: 2));
        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 3, null)), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("Only 2", response.Message);
    }

    [Fact]
    public async Task Exactly_the_available_stock_is_fine()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10, stock: 2));
        Assert.Equal(200, (await rig.Service.InitiateCheckoutAsync(Cart(("p1", 2, null)), rig.Member)).Code);
    }

    [Fact]
    public async Task Unlimited_products_ignore_stock()
    {
        var rig = new Rig();
        var product = Product("p1", 10, stock: 0); product.TrackStock = false;
        await rig.AddProduct(product);
        Assert.Equal(200, (await rig.Service.InitiateCheckoutAsync(Cart(("p1", 500, null)), rig.Member)).Code);
    }

    // ── Variants: base price + adjustment ───────────────────────────────

    private static StoreProductVariant Variant(string id, decimal adjustment, int stock = 5, string size = "M") => new()
    {
        Id = id, PriceAdjustment = adjustment, QuantityAvailable = stock, Options = new() { ["Size"] = size }, Sku = $"SKU-{id}",
    };

    private static StoreProduct VariantProduct(decimal price = 1000) { var p = Product("p1", price); p.VariantOptionTypes = ["Size"]; return p; }

    [Theory]
    [InlineData("v-base", 1000)]
    [InlineData("v-mid", 2000)]
    [InlineData("v-top", 2400)]
    public async Task The_three_variant_example_sells_at_base_plus_each_adjustment(string variantId, double expectedUnit)
    {
        var rig = new Rig();
        await rig.AddProduct(VariantProduct(1000), Variant("v-base", 0), Variant("v-mid", 1000), Variant("v-top", 1400));

        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 2, variantId)), rig.Member);

        Assert.Equal(200, response.Code);
        var order = await rig.OnlyOrder();
        var item = order.Items.Single();
        Assert.Equal((decimal)expectedUnit, item.UnitPrice);
        Assert.Equal((decimal)expectedUnit * 2, order.TotalAmount);
        Assert.Equal((long)((decimal)expectedUnit * 2 * 100), rig.Initialized.Single().Amount);
        Assert.Equal((variantId, $"SKU-{variantId}"), (item.VariantId, item.Sku));
        Assert.Equal("M", item.VariantOptions!["Size"]);
    }

    [Fact]
    public async Task A_discounted_variant_with_a_negative_adjustment_sells_below_the_base_price()
    {
        var rig = new Rig();
        await rig.AddProduct(VariantProduct(1000), Variant("v1", -250));
        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, "v1")), rig.Member);
        Assert.Equal(750m, (await rig.OnlyOrder()).TotalAmount);
    }

    [Fact]
    public async Task A_variant_product_needs_a_variant_chosen()
    {
        var rig = new Rig();
        await rig.AddProduct(VariantProduct(), Variant("v1", 0));
        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("choose an option", response.Message);
    }

    [Fact]
    public async Task A_variant_that_belongs_to_a_different_product_is_rejected()
    {
        var rig = new Rig();
        await rig.AddProduct(VariantProduct(), Variant("mine", 0));
        var other = Product("p2", 5); other.VariantOptionTypes = ["Size"];
        await rig.AddProduct(other, Variant("theirs", 0));

        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, "theirs")), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Contains("no longer available", response.Message);
    }

    [Fact]
    public async Task An_unknown_variant_is_rejected()
    {
        var rig = new Rig();
        await rig.AddProduct(VariantProduct(), Variant("v1", 0));
        Assert.Equal(400, (await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, "ghost")), rig.Member)).Code);
    }

    [Fact]
    public async Task Variant_stock_not_product_stock_is_what_limits_a_variant_purchase()
    {
        var rig = new Rig();
        var product = VariantProduct(); product.QuantityAvailable = 100;
        await rig.AddProduct(product, Variant("v1", 0, stock: 1));

        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 2, "v1")), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Contains("Only 1", response.Message);
    }

    [Fact]
    public async Task A_variant_image_is_preferred_over_the_product_image()
    {
        var rig = new Rig();
        var product = VariantProduct(); product.ImageUrls = ["https://product"];
        var variant = Variant("v1", 0); variant.ImageUrl = "https://variant";
        await rig.AddProduct(product, variant);
        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, "v1")), rig.Member);
        Assert.Equal("https://variant", (await rig.OnlyOrder()).Items.Single().ProductImageUrl);
    }

    [Fact]
    public async Task A_simple_product_ignores_a_stray_variant_id()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10));
        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, "whatever")), rig.Member);
        Assert.Equal(200, response.Code);
        Assert.Null((await rig.OnlyOrder()).Items.Single().VariantId);
    }

    // ── Order and delivery questions ────────────────────────────────────

    private static ServiceFieldDefinition Q(string key, string type = "Text", bool required = false, List<string>? options = null) =>
        new() { Key = key, Label = key.ToUpperInvariant(), Type = type, Required = required, Options = options };

    private static CheckoutRequest WithAnswers(Dictionary<string, string>? answers = null, Dictionary<string, string>? delivery = null) => new()
    {
        Items = [new CheckoutItemRequest { ProductId = "p1", Quantity = 1, Answers = answers, DeliveryAnswers = delivery }],
    };

    private static async Task<Rig> WithQuestions(List<ServiceFieldDefinition>? fields = null, List<ServiceFieldDefinition>? delivery = null)
    {
        var rig = new Rig();
        var product = Product("p1", 10); product.Fields = fields ?? []; product.DeliveryFields = delivery ?? [];
        await rig.AddProduct(product);
        return rig;
    }

    [Fact]
    public async Task Answers_are_saved_with_the_labels_as_they_were_at_purchase_time()
    {
        var rig = await WithQuestions([Q("name", required: true)], [Q("address")]);

        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(new() { ["name"] = "  Ama  " }, new() { ["address"] = "Tarkwa" }), rig.Member);

        Assert.Equal(200, response.Code);
        var answers = (await rig.OnlyOrder()).Items.Single().Answers;
        Assert.Equal(2, answers.Count);
        var name = answers.Single(a => a.Key == "name");
        Assert.Equal(("Details", "NAME", "Ama"), (name.Section, name.Label, name.Value));   // trimmed
        Assert.Equal(("Delivery", "Tarkwa"), (answers.Single(a => a.Key == "address").Section, answers.Single(a => a.Key == "address").Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_required_question_left_blank_is_rejected_with_its_label(string? value)
    {
        var rig = await WithQuestions([Q("name", required: true)]);
        var answers = value is null ? null : new Dictionary<string, string> { ["name"] = value };

        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(answers), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Contains("\"NAME\"", response.Message);
        Assert.Equal(0, await rig.Db().StoreOrders.CountAsync());
    }

    [Fact]
    public async Task A_required_delivery_question_is_enforced_too()
    {
        var rig = await WithQuestions(delivery: [Q("address", required: true)]);
        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("ADDRESS", response.Message);
    }

    [Fact]
    public async Task An_optional_question_left_blank_is_simply_not_recorded()
    {
        var rig = await WithQuestions([Q("note")]);
        Assert.Equal(200, (await rig.Service.InitiateCheckoutAsync(WithAnswers(), rig.Member)).Code);
        Assert.Empty((await rig.OnlyOrder()).Items.Single().Answers);
    }

    [Fact]
    public async Task A_select_answer_must_be_one_of_the_options()
    {
        var rig = await WithQuestions([Q("size", "Select", options: ["S", "M", "L"])]);

        Assert.Equal(400, (await rig.Service.InitiateCheckoutAsync(WithAnswers(new() { ["size"] = "XL" }), rig.Member)).Code);
        Assert.Equal(200, (await rig.Service.InitiateCheckoutAsync(WithAnswers(new() { ["size"] = "M" }), rig.Member)).Code);
    }

    [Theory]
    [InlineData("12", true)]
    [InlineData("12.5", true)]
    [InlineData("1,200", true)]
    [InlineData("-3", true)]
    [InlineData("twelve", false)]
    [InlineData("12abc", false)]
    public async Task A_number_answer_must_parse_with_invariant_culture(string value, bool ok)
    {
        var rig = await WithQuestions([Q("qty", "Number")]);
        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(new() { ["qty"] = value }), rig.Member);
        Assert.Equal(ok ? 200 : 400, response.Code);
    }

    [Theory]
    [InlineData("2026-12-25", true)]
    [InlineData("25 December 2026", true)]
    [InlineData("not a date", false)]
    [InlineData("2026-13-45", false)]
    public async Task A_date_answer_must_be_a_valid_date(string value, bool ok)
    {
        var rig = await WithQuestions([Q("when", "Date")]);
        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(new() { ["when"] = value }), rig.Member);
        Assert.Equal(ok ? 200 : 400, response.Code);
    }

    [Fact]
    public async Task A_required_file_question_without_a_file_is_rejected()
    {
        var rig = await WithQuestions([Q("proof", "File", required: true)]);
        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("attach", response.Message);
    }

    [Fact]
    public async Task An_optional_file_question_without_a_file_is_skipped()
    {
        var rig = await WithQuestions([Q("proof", "File")]);
        Assert.Equal(200, (await rig.Service.InitiateCheckoutAsync(WithAnswers(), rig.Member)).Code);
        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task An_empty_file_counts_as_no_file()
    {
        var rig = await WithQuestions([Q("proof", "File", required: true)]);
        var files = new Dictionary<string, IFormFile> { ["0:details:proof"] = File(length: 0) };
        Assert.Equal(400, (await rig.Service.InitiateCheckoutAsync(WithAnswers(), rig.Member, files)).Code);
    }

    [Fact]
    public async Task An_uploaded_file_is_stored_under_the_institution_slug_and_its_url_saved_as_the_answer()
    {
        var rig = await WithQuestions([Q("proof", "File", required: true)], [Q("id", "File")]);
        var files = new Dictionary<string, IFormFile> { ["0:details:proof"] = File("receipt.PDF"), ["0:delivery:id"] = File("id.png") };

        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(), rig.Member, files);

        Assert.Equal(200, response.Code);
        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.Is<string>(n => n.EndsWith(".PDF")), "alumni", "umat"), Times.Once);
        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.Is<string>(n => n.EndsWith(".png")), "alumni", "umat"), Times.Once);
        var answers = (await rig.OnlyOrder()).Items.Single().Answers;
        Assert.All(answers, a => Assert.Equal("https://cdn/file.pdf", a.Value));
        Assert.Equal(2, answers.Count);
    }

    [Fact]
    public async Task File_keys_are_scoped_to_the_cart_line_index()
    {
        var rig = new Rig();
        var product = Product("p1", 10); product.Fields = [Q("proof", "File", required: true)];
        await rig.AddProduct(product);
        var cart = Cart(("p1", 1, null), ("p1", 1, null));
        var files = new Dictionary<string, IFormFile> { ["0:details:proof"] = File() };   // line 1 has no file

        var response = await rig.Service.InitiateCheckoutAsync(cart, rig.Member, files);

        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task A_rejected_form_never_leaves_stray_uploads_behind()
    {
        var rig = await WithQuestions([Q("proof", "File"), Q("name", required: true)]);
        var files = new Dictionary<string, IFormFile> { ["0:details:proof"] = File() };

        var response = await rig.Service.InitiateCheckoutAsync(WithAnswers(), rig.Member, files);   // name missing

        Assert.Equal(400, response.Code);
        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Each_cart_line_is_validated_against_its_own_products_questions()
    {
        var rig = new Rig();
        var withQuestion = Product("a", 10); withQuestion.Fields = [Q("name", required: true)];
        await rig.AddProduct(withQuestion);
        await rig.AddProduct(Product("b", 10));
        var cart = new CheckoutRequest
        {
            Items =
            [
                new CheckoutItemRequest { ProductId = "b", Quantity = 1 },
                new CheckoutItemRequest { ProductId = "a", Quantity = 1 },   // no answer
            ],
        };

        Assert.Equal(400, (await rig.Service.InitiateCheckoutAsync(cart, rig.Member)).Code);
    }

    // ── Paystack outcomes ───────────────────────────────────────────────

    [Fact]
    public async Task When_paystack_declines_the_order_is_marked_failed_with_the_reason()
    {
        var rig = new Rig(paystackAccepts: false);
        await rig.AddProduct(Product("p1", 10));

        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Equal("Invalid key", response.Message);
        var order = await rig.OnlyOrder();
        Assert.Equal(("Failed", "Invalid key"), (order.Status, order.FailureMessage));
    }

    [Fact]
    public async Task The_pending_order_exists_before_paystack_is_told_about_it()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10));
        var existedWhenCalled = false;
        rig.Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()))
            .Returns<InitializePaymentRequest>(async r =>
            {
                existedWhenCalled = await rig.Db().StoreOrders.AnyAsync(o => o.TransactionRef == r.Reference && o.Status == "Pending");
                return new InitializePaymentResponse { Status = true, Data = new InitializePaymentData() };
            });

        await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        Assert.True(existedWhenCalled);
    }

    [Fact]
    public async Task An_unexpected_exception_becomes_a_500_not_a_crash()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10));
        rig.Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>())).ThrowsAsync(new HttpRequestException("boom"));

        var response = await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member);

        Assert.Equal(500, response.Code);
    }

    // ── Catalogue ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_catalogue_lists_only_active_products_with_their_variants()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 100), Variant("v1", 0), Variant("v2", 50));
        var draft = Product("p2", 5); draft.Status = "Draft";
        await rig.AddProduct(draft);

        var response = await rig.Service.GetProductsAsync(new StoreProductFilter());

        var dto = Assert.Single(response.Data!.Results);
        Assert.Equal("p1", dto.Id);
        Assert.Equal(new[] { 100m, 150m }, dto.Variants.Select(v => v.Price).OrderBy(x => x));
    }

    [Fact]
    public async Task The_catalogue_supports_word_search_and_paging()
    {
        var rig = new Rig();
        foreach (var (id, name) in new[] { ("1", "Alumni Hoodie"), ("2", "Alumni Mug"), ("3", "Campus Cap") })
        {
            var p = Product(id, 10); p.Name = name;
            await rig.AddProduct(p);
        }

        var search = await rig.Service.GetProductsAsync(new StoreProductFilter { Search = "ALUMNI hoodie" });
        Assert.Equal(new[] { "1" }, search.Data!.Results.Select(r => r.Id));

        var page = await rig.Service.GetProductsAsync(new StoreProductFilter { Page = 2, PageSize = 2, SortColumn = "Name", SortDir = "asc" });
        Assert.Single(page.Data!.Results);
        Assert.Equal(3, page.Data.TotalCount);
    }

    [Fact]
    public async Task GetProductById_returns_active_products_and_404s_otherwise()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10), Variant("v1", 5));
        var draft = Product("p2", 10); draft.Status = "Draft";
        await rig.AddProduct(draft);

        var found = await rig.Service.GetProductByIdAsync("p1");
        Assert.Equal(200, found.Code);
        Assert.Equal(15m, found.Data!.Variants.Single().Price);
        Assert.Equal(404, (await rig.Service.GetProductByIdAsync("p2")).Code);
        Assert.Equal(404, (await rig.Service.GetProductByIdAsync("ghost")).Code);
    }

    // ── Order status and history ────────────────────────────────────────

    private static async Task<(Rig rig, string reference)> PlaceOrder(Action<StoreOrder>? tweak = null)
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10));
        var reference = (await rig.Service.InitiateCheckoutAsync(Cart(("p1", 1, null)), rig.Member)).Data!.Reference;
        if (tweak is not null)
        {
            using var db = rig.Db();
            var order = await db.StoreOrders.SingleAsync();
            tweak(order);
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        return (rig, reference);
    }

    [Fact]
    public async Task OwnsReference_is_true_only_for_a_known_store_reference_and_works_across_tenants()
    {
        var (rig, reference) = await PlaceOrder();
        Assert.True(await rig.Service.OwnsReferenceAsync(reference));
        Assert.False(await rig.Service.OwnsReferenceAsync("SO_unknown"));
        Assert.False(await rig.Service.OwnsReferenceAsync("CN_" + Guid.NewGuid().ToString("N")));
    }

    [Theory]
    [InlineData("Successful", "Payment confirmed")]
    [InlineData("Pending", "Payment has been initiated but not yet completed.")]
    public async Task Status_reports_the_current_state_with_a_friendly_message(string status, string message)
    {
        var (rig, reference) = await PlaceOrder(o => o.Status = status);

        var response = await rig.Service.GetOrderStatusAsync(reference, rig.Member);

        Assert.Equal(200, response.Code);
        Assert.Equal((status, message, 10m), (response.Data!.Status, response.Data.Message, response.Data.Amount));
    }

    [Fact]
    public async Task A_failed_order_reports_its_failure_message_or_a_default()
    {
        var (rig, reference) = await PlaceOrder(o => { o.Status = "Failed"; o.FailureMessage = "Card declined"; });
        Assert.Equal("Card declined", (await rig.Service.GetOrderStatusAsync(reference, rig.Member)).Data!.Message);

        var (rig2, reference2) = await PlaceOrder(o => { o.Status = "Failed"; o.FailureMessage = null; });
        Assert.Equal("Payment failed", (await rig2.Service.GetOrderStatusAsync(reference2, rig2.Member)).Data!.Message);
    }

    [Fact]
    public async Task Status_of_an_unknown_reference_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.GetOrderStatusAsync("SO_none", rig.Member)).Code);
    }

    [Fact]
    public async Task A_member_cannot_read_another_members_order_status()
    {
        var (rig, reference) = await PlaceOrder();
        var stranger = new AuthData { Id = "someone-else" };

        var response = await rig.Service.GetOrderStatusAsync(reference, stranger);

        Assert.Equal(400, response.Code);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task A_pending_order_is_not_polled_through_temporal_when_temporal_is_unavailable()
    {
        var (rig, reference) = await PlaceOrder();
        var response = await rig.Service.GetOrderStatusAsync(reference, rig.Member);
        Assert.Equal("Pending", response.Data!.Status);
        rig.Temporal.VerifyGet(t => t.Client, Times.Never);
    }

    [Fact]
    public async Task My_orders_returns_only_the_callers_orders_newest_first_with_name_filled_from_the_member_record()
    {
        var rig = new Rig();
        await rig.AddProduct(Product("p1", 10));
        using (var db = rig.Db())
        {
            db.Members.Add(new MemberEntity { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "current@x.com" });
            db.StoreOrders.AddRange(
                new StoreOrder { Id = "o-old", MemberId = "m1", OrderNumber = "OLD", CreatedAt = DateTime.UtcNow.AddDays(-2), Member = new MemberSnapshot { FirstName = "Old", LastName = "Name" } },
                new StoreOrder { Id = "o-new", MemberId = "m1", OrderNumber = "NEW", CreatedAt = DateTime.UtcNow },
                new StoreOrder { Id = "o-other", MemberId = "m2", OrderNumber = "OTHER" });
            await db.SaveChangesAsync();
        }

        var response = await rig.Service.GetMyOrdersAsync(new StoreOrderFilter(), "m1");

        Assert.Equal(new[] { "NEW", "OLD" }, response.Data!.Results.Select(o => o.OrderNumber));
        Assert.All(response.Data.Results, o => Assert.Equal(("Ama Mensah", "current@x.com"), (o.MemberName, o.MemberEmail)));
    }

    [Fact]
    public async Task My_orders_is_empty_for_a_member_with_none()
    {
        var rig = new Rig();
        var response = await rig.Service.GetMyOrdersAsync(new StoreOrderFilter(), "nobody");
        Assert.Equal(200, response.Code);
        Assert.Empty(response.Data!.Results);
    }
}
