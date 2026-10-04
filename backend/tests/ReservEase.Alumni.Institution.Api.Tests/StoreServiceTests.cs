using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class StoreServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IStorageService> Storage { get; } = new();
        public Mock<ITemporalClientProvider> Temporal { get; } = new();
        public AuthData Admin { get; } = new() { Id = "admin-1", FirstName = "Kojo", LastName = "Staff" };
        public StoreService Service { get; private set; } = null!;

        public Rig(Action<InstitutionEntity>? institution = null)
        {
            Storage.Setup(s => s.BulkUploadFilesAsync(It.IsAny<List<IFormFile>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((List<IFormFile> files, string _, string _) => files.Select((_, i) => $"https://cdn/img{i}.png").ToList());
            Storage.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://cdn/att.pdf");
            Temporal.SetupGet(t => t.IsAvailable).Returns(false);

            var inst = new InstitutionEntity { Id = Tenant, Slug = "umat", Name = "UMaT", StoreDeliveryStages = ["Packed", "Shipped", "Delivered"] };
            institution?.Invoke(inst);
            using (var seed = TestDb.Create(DbName, Tenant)) { seed.Institutions.Add(inst); seed.SaveChanges(); }
            Fresh();
        }

        public StoreService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new StoreService(
                new AlumniPgRepository<StoreProduct>(db), new AlumniPgRepository<StoreOrder>(db), new AlumniPgRepository<StoreProductVariant>(db),
                new AlumniPgRepository<StoreProductTemplate>(db), new AlumniPgRepository<InstitutionEntity>(db), new AlumniPgRepository<MemberEntity>(db),
                Storage.Object, Temporal.Object, TestDb.Tenant(Tenant, "umat"), NullLogger<StoreService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }
    }

    /// <summary>A file shaped like a real multipart upload (with headers), since the service logs the request body.</summary>
    private static FormFile Upload(string name, int length = 3) =>
        new(new MemoryStream(new byte[length]), 0, length, "file", name) { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };

    private static VariantRequest V(decimal adjustment, int qty = 5, string size = "M", string? sku = null) =>
        new() { OptionsJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["Size"] = size }), PriceAdjustment = adjustment, QuantityAvailable = qty, Sku = sku };

    private static CreateStoreProductRequest Create(decimal price = 1000, Action<CreateStoreProductRequest>? tweak = null)
    {
        var r = new CreateStoreProductRequest { Name = "Hoodie", Price = price, QuantityAvailable = 10 };
        tweak?.Invoke(r);
        return r;
    }

    // ── Create: validation ──────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_product_price_must_be_positive(double price)
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create((decimal)price), rig.Admin);
        Assert.Equal(400, response.Code);
        Assert.Contains("Price", response.Message);
        Assert.Equal(0, await rig.Db().StoreProducts.CountAsync());
    }

    [Fact]
    public async Task Negative_stock_is_rejected()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(tweak: r => r.QuantityAvailable = -1), rig.Admin);
        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task A_simple_product_is_created_with_the_entered_price_and_stock()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(25.5m, r => { r.Description = "Warm"; r.Status = "Draft"; }), rig.Admin);

        Assert.Equal(201, response.Code);
        var saved = await rig.Db().StoreProducts.SingleAsync();
        Assert.Equal(("Hoodie", 25.5m, 10, "Draft", "admin-1"), (saved.Name, saved.Price, saved.QuantityAvailable, saved.Status, saved.CreatedBy));
        Assert.Empty(response.Data!.Variants);
    }

    [Fact]
    public async Task A_blank_delivery_note_falls_back_to_the_institution_default_but_an_explicit_one_wins()
    {
        var rig = new Rig(i => i.DefaultStoreDeliveryInfo = "Collect from the registry");

        await rig.Service.CreateProductAsync(Create(tweak: r => r.Name = "A"), rig.Admin);
        await rig.Service.CreateProductAsync(Create(tweak: r => { r.Name = "B"; r.DeliveryInfo = "We deliver"; }), rig.Admin);

        var products = await rig.Db().StoreProducts.ToDictionaryAsync(p => p.Name);
        Assert.Equal("Collect from the registry", products["A"].DeliveryInfo);
        Assert.Equal("We deliver", products["B"].DeliveryInfo);
    }

    // ── Variants: base price plus extra amount ──────────────────────────

    [Fact]
    public async Task The_three_variant_example_stores_base_1000_with_extras_0_1000_and_1400_and_reports_prices_1000_2000_2400()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(1000, r =>
        {
            r.VariantOptionTypes = ["Size"];
            r.Variants = [V(0, size: "S"), V(1000, size: "M"), V(1400, size: "L")];
        }), rig.Admin);

        Assert.Equal(201, response.Code);
        Assert.Equal(new[] { 1000m, 2000m, 2400m }, response.Data!.Variants.Select(v => v.Price).OrderBy(x => x));
        Assert.Equal(new[] { 0m, 1000m, 1400m }, response.Data.Variants.Select(v => v.PriceAdjustment).OrderBy(x => x));
        Assert.Equal(1000m, response.Data.Price);   // the base price stays what the admin entered, never rolled up
        var saved = await rig.Db().StoreProducts.SingleAsync();
        Assert.Equal(1000m, saved.Price);
        Assert.Equal(3, await rig.Db().StoreProductVariants.CountAsync());
    }

    [Fact]
    public async Task The_products_stock_becomes_the_sum_of_its_variants_stock()
    {
        var rig = new Rig();
        await rig.Service.CreateProductAsync(Create(100, r =>
        {
            r.QuantityAvailable = 999;
            r.VariantOptionTypes = ["Size"];
            r.Variants = [V(0, qty: 3, size: "S"), V(0, qty: 4, size: "M")];
        }), rig.Admin);

        Assert.Equal(7, (await rig.Db().StoreProducts.SingleAsync()).QuantityAvailable);
    }

    [Fact]
    public async Task Variant_options_keep_their_exact_case_and_sku()
    {
        var rig = new Rig();
        await rig.Service.CreateProductAsync(Create(100, r => { r.VariantOptionTypes = ["Size"]; r.Variants = [V(0, size: "Medium", sku: "TEE-M")]; }), rig.Admin);

        var variant = await rig.Db().StoreProductVariants.SingleAsync();
        Assert.Equal("Medium", variant.Options["Size"]);
        Assert.Equal("TEE-M", variant.Sku);
        Assert.Equal("admin-1", variant.CreatedBy);
    }

    [Theory]
    [InlineData(1000, -1000)]   // exactly free
    [InlineData(1000, -1500)]   // negative
    [InlineData(10, -10)]
    public async Task A_variant_that_would_end_up_free_or_negative_rejects_the_whole_product(double basePrice, double adjustment)
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create((decimal)basePrice, r =>
        {
            r.VariantOptionTypes = ["Size"];
            r.Variants = [V(0, size: "S"), V((decimal)adjustment, size: "M")];
        }), rig.Admin);

        Assert.Equal(400, response.Code);
        Assert.Contains("greater than zero", response.Message);
        Assert.Equal(0, await rig.Db().StoreProducts.CountAsync());
    }

    [Fact]
    public async Task A_negative_extra_is_fine_while_the_final_price_stays_positive()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(1000, r => { r.VariantOptionTypes = ["Size"]; r.Variants = [V(-250)]; }), rig.Admin);
        Assert.Equal(201, response.Code);
        Assert.Equal(750m, response.Data!.Variants.Single().Price);
    }

    [Fact]
    public async Task Variants_submitted_without_option_types_are_ignored_and_the_product_stays_simple()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(100, r => r.Variants = [V(-500)]), rig.Admin);

        Assert.Equal(201, response.Code);
        Assert.Equal(0, await rig.Db().StoreProductVariants.CountAsync());
        Assert.Equal(10, (await rig.Db().StoreProducts.SingleAsync()).QuantityAvailable);
    }

    // ── Create: configuration ───────────────────────────────────────────

    [Fact]
    public async Task Details_questions_delivery_questions_and_stages_are_validated_and_saved()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(50, r =>
        {
            r.PriceLabel = "  per night ";
            r.TrackStock = false;
            r.DetailsJson = "[{\"label\":\"Check-in\",\"value\":\"2pm\"}]";
            r.FieldsJson = "[{\"label\":\"Guest name\",\"type\":\"Text\",\"required\":true}]";
            r.DeliveryFieldsJson = "[{\"label\":\"Arrival time\",\"type\":\"Select\",\"options\":[\"Morning\",\"Evening\"]}]";
            r.StagesJson = "[\"Booked\",\" booked \",\"Checked in\"]";
        }), rig.Admin);

        Assert.Equal(201, response.Code);
        var saved = await rig.Db().StoreProducts.SingleAsync();
        Assert.Equal(("per night", false), (saved.PriceLabel, saved.TrackStock));
        Assert.Equal("2pm", saved.Details.Single().Value);
        Assert.Equal(("guest_name", true), (saved.Fields.Single().Key, saved.Fields.Single().Required));
        Assert.Equal(new[] { "Morning", "Evening" }, saved.DeliveryFields.Single().Options);
        Assert.Equal(new[] { "Booked", "Checked in" }, saved.Stages);
    }

    [Theory]
    [InlineData("details", "not json", "The product details could not be read.")]
    [InlineData("fields", "not json", "The order questions could not be read.")]
    [InlineData("delivery", "not json", "The delivery questions could not be read.")]
    [InlineData("stages", "not json", "The stages could not be read.")]
    [InlineData("fields", "[{\"label\":\"\",\"type\":\"Text\"}]", "Every order question needs a label.")]
    [InlineData("delivery", "[{\"label\":\"X\",\"type\":\"Select\"}]", "\"X\" is a choice question, so it needs at least one option.")]
    [InlineData("fields", "[{\"label\":\"X\",\"type\":\"Bogus\"}]", "\"X\" has an unknown type \"Bogus\".")]
    public async Task A_bad_configuration_is_rejected_with_a_specific_message_and_nothing_is_saved(string part, string json, string message)
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(50, r =>
        {
            switch (part)
            {
                case "details": r.DetailsJson = json; break;
                case "fields": r.FieldsJson = json; break;
                case "delivery": r.DeliveryFieldsJson = json; break;
                default: r.StagesJson = json; break;
            }
        }), rig.Admin);

        Assert.Equal(400, response.Code);
        Assert.Equal(message, response.Message);
        Assert.Equal(0, await rig.Db().StoreProducts.CountAsync());
    }

    [Fact]
    public async Task Uploaded_images_are_stored_under_the_institution_slug_and_their_urls_saved()
    {
        var rig = new Rig();
        var file = Upload("a.png");

        await rig.Service.CreateProductAsync(Create(tweak: r => r.Images = [file]), rig.Admin);

        rig.Storage.Verify(s => s.BulkUploadFilesAsync(It.Is<List<IFormFile>>(l => l.Count == 1), "", "umat"), Times.Once);
        Assert.Equal(new[] { "https://cdn/img0.png" }, (await rig.Db().StoreProducts.SingleAsync()).ImageUrls);
    }

    // ── Update ──────────────────────────────────────────────────────────

    private static async Task<(Rig rig, string id)> ProductWithVariants()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateProductAsync(Create(1000, r => { r.VariantOptionTypes = ["Size"]; r.Variants = [V(0, size: "S"), V(1000, size: "M")]; }), rig.Admin);
        rig.Fresh();
        return (rig, response.Data!.Id);
    }

    private static UpdateStoreProductRequest Update(string id, decimal price = 1000, Action<UpdateStoreProductRequest>? tweak = null)
    {
        var r = new UpdateStoreProductRequest { ProductId = id, Name = "Hoodie v2", Price = price, QuantityAvailable = 4 };
        tweak?.Invoke(r);
        return r;
    }

    [Fact]
    public async Task Changing_the_base_price_moves_every_variant_with_it()
    {
        var (rig, id) = await ProductWithVariants();

        var response = await rig.Service.UpdateProductAsync(Update(id, 1500, r => { r.VariantOptionTypes = ["Size"]; r.Variants = [V(0, size: "S"), V(1000, size: "M")]; }), rig.Admin);

        Assert.Equal(200, response.Code);
        Assert.Equal(new[] { 1500m, 2500m }, response.Data!.Variants.Select(v => v.Price).OrderBy(x => x));
    }

    [Fact]
    public async Task Editing_replaces_the_variant_set_wholesale()
    {
        var (rig, id) = await ProductWithVariants();

        await rig.Service.UpdateProductAsync(Update(id, 1000, r => { r.VariantOptionTypes = ["Size"]; r.Variants = [V(500, qty: 2, size: "XL")]; }), rig.Admin);

        var variants = await rig.Db().StoreProductVariants.ToListAsync();
        var only = Assert.Single(variants);
        Assert.Equal(("XL", 500m), (only.Options["Size"], only.PriceAdjustment));
        Assert.Equal(2, (await rig.Db().StoreProducts.SingleAsync()).QuantityAvailable);
    }

    [Fact]
    public async Task Clearing_the_option_types_turns_the_product_back_into_a_simple_one_with_the_entered_stock()
    {
        var (rig, id) = await ProductWithVariants();

        var response = await rig.Service.UpdateProductAsync(Update(id, 1200), rig.Admin);

        Assert.Equal(200, response.Code);
        Assert.Equal(0, await rig.Db().StoreProductVariants.CountAsync());
        var saved = await rig.Db().StoreProducts.SingleAsync();
        Assert.Equal((1200m, 4, true), (saved.Price, saved.QuantityAvailable, saved.VariantOptionTypes.Count == 0));
    }

    [Fact]
    public async Task Lowering_the_base_price_so_a_variant_goes_non_positive_is_rejected_and_the_old_data_stays()
    {
        var (rig, id) = await ProductWithVariants();

        var response = await rig.Service.UpdateProductAsync(Update(id, 100, r => { r.VariantOptionTypes = ["Size"]; r.Variants = [V(-100, size: "S")]; }), rig.Admin);

        Assert.Equal(400, response.Code);
        Assert.Equal(1000m, (await rig.Db().StoreProducts.SingleAsync()).Price);
        Assert.Equal(2, await rig.Db().StoreProductVariants.CountAsync());
    }

    [Fact]
    public async Task Update_of_an_unknown_product_is_404_and_validation_still_applies()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.UpdateProductAsync(Update("ghost"), rig.Admin)).Code);

        await rig.Service.CreateProductAsync(Create(), rig.Admin);
        rig.Fresh();
        var id = (await rig.Db().StoreProducts.SingleAsync()).Id;
        Assert.Equal(400, (await rig.Service.UpdateProductAsync(Update(id, 0), rig.Admin)).Code);
        Assert.Equal(400, (await rig.Service.UpdateProductAsync(Update(id, 10, r => r.QuantityAvailable = -1), rig.Admin)).Code);
    }

    [Fact]
    public async Task A_null_config_on_update_leaves_that_part_unchanged_while_an_explicit_empty_list_clears_it()
    {
        var rig = new Rig();
        var created = await rig.Service.CreateProductAsync(Create(50, r => { r.StagesJson = "[\"A\",\"B\"]"; r.FieldsJson = "[{\"label\":\"Q\",\"type\":\"Text\"}]"; }), rig.Admin);
        rig.Fresh();

        await rig.Service.UpdateProductAsync(Update(created.Data!.Id, 50), rig.Admin);   // JSON properties null
        var kept = await rig.Db().StoreProducts.SingleAsync();
        Assert.Equal(new[] { "A", "B" }, kept.Stages);
        Assert.Single(kept.Fields);

        rig.Fresh();
        await rig.Service.UpdateProductAsync(Update(created.Data.Id, 50, r => { r.StagesJson = "[]"; r.FieldsJson = ""; }), rig.Admin);
        var cleared = await rig.Db().StoreProducts.SingleAsync();
        Assert.Empty(cleared.Stages);
        Assert.Empty(cleared.Fields);
    }

    [Fact]
    public async Task Update_keeps_existing_images_and_appends_new_ones_stamping_who_edited()
    {
        var rig = new Rig();
        var created = await rig.Service.CreateProductAsync(Create(), rig.Admin);
        rig.Fresh();
        var file = Upload("b.png");

        await rig.Service.UpdateProductAsync(Update(created.Data!.Id, 10, r => { r.ExistingImageUrls = ["https://old/1.png"]; r.Images = [file]; }), rig.Admin);

        var saved = await rig.Db().StoreProducts.SingleAsync();
        Assert.Equal(new[] { "https://old/1.png", "https://cdn/img0.png" }, saved.ImageUrls);
        Assert.Equal(("admin-1", true), (saved.UpdatedBy, saved.UpdatedAt.HasValue));
    }

    [Fact]
    public async Task Removing_every_image_clears_the_list()
    {
        var rig = new Rig();
        var created = await rig.Service.CreateProductAsync(Create(tweak: r => r.Images = [Upload("a.png")]), rig.Admin);
        rig.Fresh();
        await rig.Service.UpdateProductAsync(Update(created.Data!.Id, 10), rig.Admin);
        Assert.Null((await rig.Db().StoreProducts.SingleAsync()).ImageUrls);
    }

    // ── Delete, list, detail ────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_product_deletes_its_variants_too()
    {
        var (rig, id) = await ProductWithVariants();

        var response = await rig.Service.DeleteProductAsync(id);

        Assert.Equal(200, response.Code);
        Assert.Equal(0, await rig.Db().StoreProducts.CountAsync());
        Assert.Equal(0, await rig.Db().StoreProductVariants.CountAsync());
        Assert.Equal(404, (await rig.Service.DeleteProductAsync(id)).Code);
    }

    [Fact]
    public async Task The_admin_list_includes_draft_products_filters_by_status_and_searches_by_words()
    {
        var rig = new Rig();
        await rig.Seed(
            new StoreProduct { Id = "1", Name = "Alumni Hoodie", Price = 10, Status = "Active" },
            new StoreProduct { Id = "2", Name = "Alumni Mug", Price = 5, Status = "Draft" },
            new StoreProduct { Id = "3", Name = "Campus Cap", Price = 8, Status = "Archived" });

        Assert.Equal(3, (await rig.Service.GetProductsAsync(new StoreProductFilter())).Data!.TotalCount);
        Assert.Equal(new[] { "2" }, (await rig.Service.GetProductsAsync(new StoreProductFilter { Status = "Draft" })).Data!.Results.Select(p => p.Id));
        Assert.Equal(new[] { "1" }, (await rig.Service.GetProductsAsync(new StoreProductFilter { Search = "HOODIE alumni" })).Data!.Results.Select(p => p.Id));
    }

    [Fact]
    public async Task Product_detail_includes_variant_prices_or_404s()
    {
        var (rig, id) = await ProductWithVariants();
        var response = await rig.Service.GetProductByIdAsync(id);
        Assert.Equal(new[] { 1000m, 2000m }, response.Data!.Variants.Select(v => v.Price).OrderBy(x => x));
        Assert.Equal(404, (await rig.Service.GetProductByIdAsync("ghost")).Code);
    }

    // ── Orders ──────────────────────────────────────────────────────────

    private static StoreOrder Order(string id, string status = "Successful", string memberId = "m1", string? delivery = null) =>
        new() { Id = id, OrderNumber = $"ORD-{id}", MemberId = memberId, Status = status, DeliveryStatus = delivery, TotalAmount = 10 };

    [Fact]
    public async Task Staff_only_see_paid_orders_filterable_by_delivery_stage_with_live_member_names()
    {
        var rig = new Rig();
        await rig.Seed(
            new MemberEntity { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "new@x.com" },
            Order("paid-a", "Successful", delivery: "Packed"), Order("paid-b", "Successful", delivery: "Shipped"),
            Order("pending", "Pending"), Order("failed", "Failed"));

        var all = await rig.Service.GetOrdersAsync(new StoreOrderFilter());
        Assert.Equal(new[] { "paid-a", "paid-b" }, all.Data!.Results.Select(o => o.Id).OrderBy(x => x));
        Assert.All(all.Data.Results, o => Assert.Equal(("Ama Mensah", "new@x.com"), (o.MemberName, o.MemberEmail)));

        var shipped = await rig.Service.GetOrdersAsync(new StoreOrderFilter { DeliveryStatus = "Shipped" });
        Assert.Equal(new[] { "paid-b" }, shipped.Data!.Results.Select(o => o.Id));
    }

    [Fact]
    public async Task A_delivery_status_must_be_one_of_the_stores_configured_stages()
    {
        var rig = new Rig();
        await rig.Seed(Order("o1"));

        var bad = await rig.Service.UpdateDeliveryStatusAsync("o1", "Teleported", rig.Admin);

        Assert.Equal(400, bad.Code);
        Assert.Contains("not one of this store's configured delivery stages", bad.Message);
        Assert.Null((await rig.Db().StoreOrders.SingleAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task A_valid_delivery_status_is_recorded_with_history_and_the_member_is_notified()
    {
        var rig = new Rig();
        await rig.Seed(Order("o1"));

        var response = await rig.Service.UpdateDeliveryStatusAsync("o1", "Shipped", rig.Admin);

        Assert.Equal(200, response.Code);
        var saved = await rig.Db().StoreOrders.SingleAsync();
        Assert.Equal(("Shipped", "admin-1"), (saved.DeliveryStatus, saved.UpdatedBy));
        Assert.Equal("Shipped", saved.DeliveryStatusHistory.Single().Status);
        Assert.NotNull(saved.DeliveryStatusUpdatedAt);
        rig.Temporal.VerifyGet(t => t.IsAvailable, Times.AtLeastOnce);   // an enqueue was attempted
    }

    [Fact]
    public async Task Clearing_the_delivery_status_with_null_adds_no_history_and_sends_no_notification()
    {
        var rig = new Rig();
        await rig.Seed(Order("o1", delivery: "Shipped"));

        var response = await rig.Service.UpdateDeliveryStatusAsync("o1", null, rig.Admin);

        Assert.Equal(200, response.Code);
        var saved = await rig.Db().StoreOrders.SingleAsync();
        Assert.Null(saved.DeliveryStatus);
        Assert.Empty(saved.DeliveryStatusHistory);
        rig.Temporal.VerifyGet(t => t.IsAvailable, Times.Never);
    }

    [Fact]
    public async Task Delivery_status_of_an_unknown_order_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.UpdateDeliveryStatusAsync("ghost", "Shipped", rig.Admin)).Code);
    }

    // ── Per-item fulfilment ─────────────────────────────────────────────

    private static StoreOrder StagedOrder(params string[] stages) =>
        new()
        {
            Id = "o1", OrderNumber = "ORD-1", MemberId = "m1", Status = "Successful",
            Items = [new StoreOrderItem { ProductId = "p1", ProductName = "Booking", Quantity = 1, UnitPrice = 10, Stages = stages.ToList(), CurrentStage = stages.FirstOrDefault() }],
        };

    [Fact]
    public async Task Moving_an_item_to_one_of_its_stages_records_an_update_with_the_staff_name()
    {
        var rig = new Rig();
        await rig.Seed(StagedOrder("Booked", "Confirmed", "Done"));

        var response = await rig.Service.UpdateOrderItemAsync("o1", 0, new UpdateStoreOrderItemRequest { Stage = "Confirmed", Note = "  Room 4 " }, rig.Admin);

        Assert.Equal(200, response.Code);
        var item = (await rig.Db().StoreOrders.SingleAsync()).Items.Single();
        Assert.Equal("Confirmed", item.CurrentStage);
        var update = Assert.Single(item.Updates);
        Assert.Equal(("Confirmed", "Room 4", "Kojo Staff"), (update.Stage, update.Note, update.ChangedByStaffName));
    }

    [Fact]
    public async Task A_note_alone_does_not_change_the_current_stage()
    {
        var rig = new Rig();
        await rig.Seed(StagedOrder("Booked", "Done"));
        await rig.Service.UpdateOrderItemAsync("o1", 0, new UpdateStoreOrderItemRequest { Note = "Called the guest" }, rig.Admin);

        var item = (await rig.Db().StoreOrders.SingleAsync()).Items.Single();
        Assert.Equal("Booked", item.CurrentStage);
        Assert.Null(item.Updates.Single().Stage);
    }

    [Fact]
    public async Task An_attachment_handed_back_is_uploaded_and_linked_on_the_update()
    {
        var rig = new Rig();
        await rig.Seed(StagedOrder("Booked", "Done"));
        var file = Upload("receipt.PDF");

        await rig.Service.UpdateOrderItemAsync("o1", 0, new UpdateStoreOrderItemRequest { Attachment = file }, rig.Admin);

        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.Is<string>(n => n.EndsWith(".PDF")), "alumni", "umat"), Times.Once);
        Assert.Equal("https://cdn/att.pdf", (await rig.Db().StoreOrders.SingleAsync()).Items.Single().Updates.Single().AttachmentUrl);
    }

    [Fact]
    public async Task An_item_without_its_own_stages_cannot_be_progressed_this_way()
    {
        var rig = new Rig();
        await rig.Seed(StagedOrder());
        var response = await rig.Service.UpdateOrderItemAsync("o1", 0, new UpdateStoreOrderItemRequest { Note = "x" }, rig.Admin);
        Assert.Equal(400, response.Code);
        Assert.Contains("no stages of its own", response.Message);
    }

    [Fact]
    public async Task An_empty_update_is_rejected_and_a_stage_outside_the_items_list_is_rejected()
    {
        var rig = new Rig();
        await rig.Seed(StagedOrder("Booked", "Done"));

        Assert.Equal(400, (await rig.Service.UpdateOrderItemAsync("o1", 0, new UpdateStoreOrderItemRequest(), rig.Admin)).Code);
        var wrong = await rig.Service.UpdateOrderItemAsync("o1", 0, new UpdateStoreOrderItemRequest { Stage = "Teleported" }, rig.Admin);
        Assert.Equal(400, wrong.Code);
        Assert.Contains("not one of this item's stages", wrong.Message);
    }

    [Theory]
    [InlineData("ghost", 0)]
    [InlineData("o1", -1)]
    [InlineData("o1", 1)]
    public async Task An_unknown_order_or_item_index_is_404(string orderId, int index)
    {
        var rig = new Rig();
        await rig.Seed(StagedOrder("Booked"));
        Assert.Equal(404, (await rig.Service.UpdateOrderItemAsync(orderId, index, new UpdateStoreOrderItemRequest { Note = "x" }, rig.Admin)).Code);
    }

    // ── Settings ────────────────────────────────────────────────────────

    [Fact]
    public async Task Store_settings_round_trip_and_null_stages_leave_the_existing_ones()
    {
        var rig = new Rig();

        var read = await rig.Service.GetSettingsAsync();
        Assert.Equal(new[] { "Packed", "Shipped", "Delivered" }, read.Data!.DeliveryStages);

        await rig.Service.UpdateSettingsAsync(new UpdateStoreSettingsRequest { DefaultDeliveryInfo = "Pickup only" }, rig.Admin);
        rig.Fresh();
        var kept = (await rig.Service.GetSettingsAsync()).Data!;
        Assert.Equal(("Pickup only", 3), (kept.DefaultDeliveryInfo, kept.DeliveryStages.Count));

        await rig.Service.UpdateSettingsAsync(new UpdateStoreSettingsRequest { DefaultDeliveryInfo = "x", DeliveryStages = ["One"] }, rig.Admin);
        rig.Fresh();
        Assert.Equal(new[] { "One" }, (await rig.Service.GetSettingsAsync()).Data!.DeliveryStages);
    }

    // ── Reusable setups (templates) ─────────────────────────────────────

    private static StoreProductTemplateRequest Template(Action<StoreProductTemplateRequest>? tweak = null)
    {
        var r = new StoreProductTemplateRequest { Name = "  Hotel booking ", PriceLabel = "per night", TrackStock = false, Stages = ["Booked", "booked", "Stayed"] };
        tweak?.Invoke(r);
        return r;
    }

    [Fact]
    public async Task A_template_is_validated_normalised_and_saved()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateTemplateAsync(Template(r =>
        {
            r.Fields = [new ServiceFieldDefinitionRequest { Label = "Guest name", Type = "Text", Required = true }];
            r.Details = [new StoreDetailItemRequest { Label = "Check-in", Value = "2pm" }];
            r.Description = "  ";
        }), rig.Admin);

        Assert.Equal(201, response.Code);
        var saved = await rig.Db().StoreProductTemplates.SingleAsync();
        Assert.Equal(("Hotel booking", "per night", false, null), (saved.Name, saved.PriceLabel, saved.TrackStock, saved.Description));
        Assert.Equal(new[] { "Booked", "Stayed" }, saved.Stages);
        Assert.Equal("guest_name", saved.Fields.Single().Key);
    }

    [Fact]
    public async Task A_template_needs_a_name_and_valid_questions()
    {
        var rig = new Rig();
        Assert.Equal(400, (await rig.Service.CreateTemplateAsync(Template(r => r.Name = "  "), rig.Admin)).Code);
        var bad = await rig.Service.CreateTemplateAsync(Template(r => r.DeliveryFields = [new ServiceFieldDefinitionRequest { Label = "", Type = "Text" }]), rig.Admin);
        Assert.Equal(400, bad.Code);
        Assert.Equal("Every delivery question needs a label.", bad.Message);
        Assert.Equal(0, await rig.Db().StoreProductTemplates.CountAsync());
    }

    [Fact]
    public async Task Templates_are_listed_by_name_and_can_be_updated_and_deleted_without_touching_products()
    {
        var rig = new Rig();
        await rig.Service.CreateTemplateAsync(Template(r => r.Name = "Zebra"), rig.Admin);
        await rig.Service.CreateTemplateAsync(Template(r => r.Name = "Apple"), rig.Admin);
        await rig.Seed(new StoreProduct { Id = "p", Name = "Made from template", Price = 1, Stages = ["Booked"] });
        rig.Fresh();

        var list = (await rig.Service.GetTemplatesAsync()).Data!;
        Assert.Equal(new[] { "Apple", "Zebra" }, list.Select(t => t.Name));

        var id = list[0].Id;
        var updated = await rig.Service.UpdateTemplateAsync(id, Template(r => r.Name = "Apricot"), rig.Admin);
        Assert.Equal("Apricot", updated.Data!.Name);
        Assert.Equal(404, (await rig.Service.UpdateTemplateAsync("ghost", Template(), rig.Admin)).Code);

        Assert.Equal(200, (await rig.Service.DeleteTemplateAsync(id)).Code);
        Assert.Equal(404, (await rig.Service.DeleteTemplateAsync(id)).Code);
        Assert.Equal(new[] { "Booked" }, (await rig.Db().StoreProducts.SingleAsync()).Stages);   // products were configured from a copy
    }
}
