using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Operations.Worker.Workflows.StoreOrders;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using Temporalio.Client;
using Temporalio.Worker;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class StoreOrderCallbackWorkflowTests(TemporalFixture temporal)
{
    private sealed class Rig : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
        public Mock<IPaystackService> Paystack { get; } = new();
        public StoreOrderCallbackActivities Activities { get; }

        public Rig()
        {
            var (db, conn) = TestDb.CreateRelational();
            connection = conn;
            db.Dispose();
            var ctx = TestDb.OpenRelational(connection);
            Activities = new StoreOrderCallbackActivities(
                new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<StoreProduct>(ctx), new AlumniPgRepository<StoreProductVariant>(ctx),
                Paystack.Object, NullLogger<StoreOrderCallbackActivities>.Instance);
        }

        public TestDbHandle Db() => new(TestDb.OpenRelational(connection));
        public void Verify(bool ok, string status, long amountSubunit = 0, long? fees = null, string message = "Verification successful") =>
            Paystack.Setup(p => p.VerifyPaymentAsync(It.IsAny<string>())).ReturnsAsync(new VerifyPaymentResponse
            {
                Status = ok, Message = message,
                Data = new VerifyPaymentData { Status = status, Amount = amountSubunit, Fees = fees, GatewayResponse = "Approved" },
            });

        public void Dispose() => connection.Dispose();
    }

    // Small wrapper so tests can query without tracking noise.
    private sealed class TestDbHandle(ReservEase.Alumni.PostgresDb.Sdk.DbContexts.AlumniDbContext inner) : IDisposable
    {
        public IQueryable<StoreOrder> Orders => inner.StoreOrders.IgnoreQueryFilters();
        public IQueryable<StoreProduct> Products => inner.StoreProducts.IgnoreQueryFilters();
        public IQueryable<StoreProductVariant> Variants => inner.StoreProductVariants.IgnoreQueryFilters();
        public ReservEase.Alumni.PostgresDb.Sdk.DbContexts.AlumniDbContext Inner => inner;
        public void Dispose() => inner.Dispose();
    }

    private async Task<PaymentCallbackResult> Run(Rig rig, string reference, string? rawBody = null)
    {
        var queue = "q-" + Guid.NewGuid().ToString("N");
        using var worker = new TemporalWorker(temporal.Client,
            new TemporalWorkerOptions(queue).AddWorkflow<ProcessStoreOrderCallbackWorkflow>().AddAllActivities(rig.Activities));
        return await worker.ExecuteAsync(() => temporal.Client.ExecuteWorkflowAsync(
            (ProcessStoreOrderCallbackWorkflow wf) => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, rawBody!)),
            new WorkflowOptions("wf-" + Guid.NewGuid().ToString("N"), queue)));
    }

    private static async Task Seed(Rig rig, Action<TestDbHandle> _ = null!, params object[] entities)
    {
        using var db = rig.Db();
        foreach (var e in entities) db.Inner.Add(e);
        await db.Inner.SaveChangesAsync();
    }

    private static StoreOrder Order(string reference = "SO_1", string status = "Pending", params StoreOrderItem[] items) =>
        new() { Id = "o1", TransactionRef = reference, Status = status, MemberId = "m1", Items = items.ToList(), TotalAmount = 100, InstitutionId = "i1" };

    private static StoreProduct Product(string id, int stock, bool track = true) =>
        new() { Id = id, Name = id, Price = 10, QuantityAvailable = stock, TrackStock = track, InstitutionId = "i1" };

    private static StoreOrderItem Item(string productId, int qty, string? variantId = null) =>
        new() { ProductId = productId, ProductName = productId, Quantity = qty, UnitPrice = 10, VariantId = variantId };

    [WorkflowFact]
    public async Task An_unknown_reference_is_rejected()
    {
        using var rig = new Rig();
        var result = await Run(rig, "SO_nope");
        Assert.True(result.IsBadRequest);
        Assert.Equal("Unknown payment reference.", result.Message);
        rig.Paystack.Verify(p => p.VerifyPaymentAsync(It.IsAny<string>()), Times.Never);
    }

    [WorkflowFact]
    public async Task An_already_successful_order_is_not_verified_or_decremented_again()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Successful", Item("p1", 2)), Product("p1", 10));

        var result = await Run(rig, "SO_1");

        Assert.False(result.IsBadRequest);
        Assert.Contains("already verified", result.Message);
        rig.Paystack.Verify(p => p.VerifyPaymentAsync(It.IsAny<string>()), Times.Never);
        using var db = rig.Db();
        Assert.Equal(10, (await db.Products.SingleAsync()).QuantityAvailable);
    }

    [WorkflowFact]
    public async Task A_successful_payment_confirms_the_order_and_decrements_stock()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 3)), Product("p1", 10));
        rig.Verify(true, "success", amountSubunit: 10250, fees: 250);

        var result = await Run(rig, "SO_1");

        Assert.False(result.IsBadRequest);
        Assert.Contains("confirmed", result.Message);
        using var db = rig.Db();
        var order = await db.Orders.SingleAsync();
        Assert.Equal("Successful", order.Status);
        Assert.NotNull(order.ConfirmedAt);
        Assert.Equal((102.50m, 2.50m, "Approved"), (order.GrossChargeAmount, order.GatewayFeeAmount, order.GatewayResponse));
        Assert.Equal(7, (await db.Products.SingleAsync()).QuantityAvailable);
    }

    [WorkflowFact]
    public async Task The_gateway_fee_is_left_alone_when_paystack_does_not_report_one()
    {
        using var rig = new Rig();
        var order = Order("SO_1", "Pending", Item("p1", 1)); order.GatewayFeeAmount = 1.75m;
        await Seed(rig, null!, order, Product("p1", 5));
        rig.Verify(true, "success", 10000, fees: null);

        await Run(rig, "SO_1");

        using var db = rig.Db();
        Assert.Equal(1.75m, (await db.Orders.SingleAsync()).GatewayFeeAmount);
    }

    [WorkflowFact]
    public async Task Stock_never_goes_below_zero_when_an_order_oversells()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 5)), Product("p1", 2));
        rig.Verify(true, "success", 5000);

        var result = await Run(rig, "SO_1");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal(0, (await db.Products.SingleAsync()).QuantityAvailable);
        Assert.Equal("Successful", (await db.Orders.SingleAsync()).Status);   // already paid, so the order still stands
    }

    [WorkflowFact]
    public async Task Unlimited_products_are_never_decremented()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("svc", 4)), Product("svc", 0, track: false));
        rig.Verify(true, "success", 4000);

        await Run(rig, "SO_1");

        using var db = rig.Db();
        Assert.Equal(0, (await db.Products.SingleAsync()).QuantityAvailable);
        Assert.Equal("Successful", (await db.Orders.SingleAsync()).Status);
    }

    [WorkflowFact]
    public async Task Several_lines_each_decrement_their_own_product()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("a", 1), Item("b", 4)), Product("a", 10), Product("b", 6));
        rig.Verify(true, "success", 5000);

        await Run(rig, "SO_1");

        using var db = rig.Db();
        var stock = await db.Products.ToDictionaryAsync(p => p.Id, p => p.QuantityAvailable);
        Assert.Equal((9, 2), (stock["a"], stock["b"]));
    }

    [WorkflowFact]
    public async Task A_missing_product_is_skipped_without_failing_the_order()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("ghost", 1)));
        rig.Verify(true, "success", 1000);

        var result = await Run(rig, "SO_1");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal("Successful", (await db.Orders.SingleAsync()).Status);
    }

    [WorkflowFact]
    public async Task A_variant_purchase_decrements_the_variant_and_rolls_the_product_stock_up_from_all_variants()
    {
        using var rig = new Rig();
        var product = Product("p1", 9); product.VariantOptionTypes = ["Size"];
        var v1 = new StoreProductVariant { Id = "v1", ProductId = "p1", QuantityAvailable = 5, InstitutionId = "i1" };
        var v2 = new StoreProductVariant { Id = "v2", ProductId = "p1", QuantityAvailable = 4, InstitutionId = "i1" };
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 2, "v1")), product, v1, v2);
        rig.Verify(true, "success", 2000);

        await Run(rig, "SO_1");

        using var db = rig.Db();
        var variants = await db.Variants.ToDictionaryAsync(v => v.Id, v => v.QuantityAvailable);
        Assert.Equal((3, 4), (variants["v1"], variants["v2"]));
        Assert.Equal(7, (await db.Products.SingleAsync()).QuantityAvailable);   // 3 + 4
    }

    [WorkflowFact]
    public async Task A_variant_oversell_clamps_at_zero()
    {
        using var rig = new Rig();
        var product = Product("p1", 1); product.VariantOptionTypes = ["Size"];
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 3, "v1")), product,
            new StoreProductVariant { Id = "v1", ProductId = "p1", QuantityAvailable = 1, InstitutionId = "i1" });
        rig.Verify(true, "success", 3000);

        await Run(rig, "SO_1");

        using var db = rig.Db();
        Assert.Equal(0, (await db.Variants.SingleAsync()).QuantityAvailable);
        Assert.Equal(0, (await db.Products.SingleAsync()).QuantityAvailable);
    }

    [WorkflowFact]
    public async Task A_failed_verification_marks_the_order_failed_and_leaves_stock_untouched()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 3)), Product("p1", 10));
        rig.Verify(false, "unknown", message: "Transaction reference not found");

        var result = await Run(rig, "SO_1");

        Assert.True(result.IsBadRequest);
        Assert.Equal("Transaction reference not found", result.Message);
        using var db = rig.Db();
        var order = await db.Orders.SingleAsync();
        Assert.Equal(("Failed", "Transaction reference not found"), (order.Status, order.FailureMessage));
        Assert.Equal(10, (await db.Products.SingleAsync()).QuantityAvailable);
    }

    [WorkflowTheory]
    [InlineData("failed")]
    [InlineData("abandoned")]
    [InlineData("pending")]
    public async Task A_non_success_paystack_status_fails_the_order_with_that_status(string paystackStatus)
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 3)), Product("p1", 10));
        rig.Verify(true, paystackStatus, 1000);

        var result = await Run(rig, "SO_1");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        var order = await db.Orders.SingleAsync();
        Assert.Equal(("Failed", $"Payment {paystackStatus}."), (order.Status, order.FailureMessage));
        Assert.Equal(10, (await db.Products.SingleAsync()).QuantityAvailable);
    }

    [WorkflowFact]
    public async Task The_raw_webhook_body_is_stored_and_the_channel_is_read_from_it()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 1)), Product("p1", 10));
        rig.Verify(true, "success", 1000);
        var body = "{\"event\":\"charge.success\",\"data\":{\"authorization\":{\"channel\":\"mobile_money\"}}}";

        await Run(rig, "SO_1", body);

        using var db = rig.Db();
        var order = await db.Orders.SingleAsync();
        Assert.Equal(body, order.CallbackPayload);
        Assert.Equal("mobile_money", order.Channel);
    }

    [WorkflowFact]
    public async Task A_malformed_webhook_body_does_not_stop_the_order_being_confirmed()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Pending", Item("p1", 1)), Product("p1", 10));
        rig.Verify(true, "success", 1000);

        var result = await Run(rig, "SO_1", "this is not json");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal("Successful", (await db.Orders.SingleAsync()).Status);
    }

    [WorkflowFact]
    public async Task Replaying_a_confirmed_orders_webhook_only_records_the_new_payload()
    {
        using var rig = new Rig();
        await Seed(rig, null!, Order("SO_1", "Successful", Item("p1", 2)), Product("p1", 10));

        await Run(rig, "SO_1", "{\"again\":true}");

        using var db = rig.Db();
        Assert.Equal("{\"again\":true}", (await db.Orders.SingleAsync()).CallbackPayload);
        Assert.Equal(10, (await db.Products.SingleAsync()).QuantityAvailable);
    }
}
