using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Operations.Worker.Workflows.ServiceRequests;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class ServiceRequestCallbackWorkflowTests(TemporalFixture temporal)
{
    private sealed class Rig : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
        public Mock<IPaystackService> Paystack { get; } = new();
        public ServiceRequestCallbackActivities Activities { get; }

        public Rig()
        {
            var (first, conn) = TestDb.CreateRelational();
            first.Dispose();
            connection = conn;
            var ctx = TestDb.OpenRelational(connection);
            Activities = new ServiceRequestCallbackActivities(
                new AlumniPgRepository<ServiceRequest>(ctx), new AlumniPgRepository<ServiceType>(ctx), Paystack.Object,
                NullLogger<ServiceRequestCallbackActivities>.Instance);
        }

        public AlumniDbContext Db() => TestDb.OpenRelational(connection);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) db.Add(e);
            await db.SaveChangesAsync();
        }

        public void Verify(bool ok, string status, long amountSubunit = 0, long? fees = null, string message = "ok") =>
            Paystack.Setup(p => p.VerifyPaymentAsync(It.IsAny<string>())).ReturnsAsync(new VerifyPaymentResponse
            {
                Status = ok, Message = message, Data = new VerifyPaymentData { Status = status, Amount = amountSubunit, Fees = fees, GatewayResponse = "Approved" },
            });

        public void Dispose() => connection.Dispose();
    }

    private Task<PaymentCallbackResult> Run(Rig rig, string reference, string? body = null) =>
        WorkflowHarness.RunCallback<ProcessServiceRequestCallbackWorkflow>(temporal.Client, reference, body, rig.Activities);

    private static ServiceRequest Request(string status = "Pending") =>
        new() { Id = "r1", TransactionRef = "SR_1", PaymentStatus = status, ServiceTypeId = "s1", MemberId = "m1", InstitutionId = "i1", Amount = 50, CurrentStage = "" };

    private static ServiceType Type(params string[] stages) =>
        new() { Id = "s1", Name = "Transcript", Price = 50, Stages = stages.ToList(), InstitutionId = "i1" };

    [WorkflowFact]
    public async Task An_unknown_reference_is_rejected()
    {
        using var rig = new Rig();
        var result = await Run(rig, "SR_nope");
        Assert.True(result.IsBadRequest);
        Assert.Equal("Unknown payment reference.", result.Message);
    }

    [WorkflowFact]
    public async Task A_successful_payment_confirms_the_request_and_starts_it_in_the_first_stage_with_a_history_entry()
    {
        using var rig = new Rig();
        await rig.Seed(Request(), Type("Received", "Processing", "Sent"));
        rig.Verify(true, "success", 5150, fees: 100);

        var result = await Run(rig, "SR_1");

        Assert.False(result.IsBadRequest);
        Assert.Contains("confirmed", result.Message);
        using var db = rig.Db();
        var saved = await db.ServiceRequests.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Successful", "Received", 51.50m, 1.00m), (saved.PaymentStatus, saved.CurrentStage, saved.GrossChargeAmount, saved.GatewayFeeAmount));
        Assert.NotNull(saved.ConfirmedAt);
        var update = Assert.Single(saved.Updates);
        Assert.Equal("Received", update.Stage);
    }

    [WorkflowFact]
    public async Task A_service_without_stages_starts_as_submitted()
    {
        using var rig = new Rig();
        await rig.Seed(Request(), Type());
        rig.Verify(true, "success", 5000);

        await Run(rig, "SR_1");

        using var db = rig.Db();
        Assert.Equal("Submitted", (await db.ServiceRequests.IgnoreQueryFilters().SingleAsync()).CurrentStage);
    }

    [WorkflowFact]
    public async Task A_deleted_service_type_still_lets_a_paid_request_be_confirmed_as_submitted()
    {
        using var rig = new Rig();
        await rig.Seed(Request());
        rig.Verify(true, "success", 5000);

        var result = await Run(rig, "SR_1");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        var saved = await db.ServiceRequests.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Successful", "Submitted"), (saved.PaymentStatus, saved.CurrentStage));
    }

    [WorkflowFact]
    public async Task An_already_confirmed_request_is_not_verified_or_restarted()
    {
        using var rig = new Rig();
        var request = Request("Successful"); request.CurrentStage = "Processing";
        await rig.Seed(request, Type("Received"));

        var result = await Run(rig, "SR_1");

        Assert.Contains("already verified", result.Message);
        rig.Paystack.Verify(p => p.VerifyPaymentAsync(It.IsAny<string>()), Times.Never);
        using var db = rig.Db();
        var saved = await db.ServiceRequests.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Processing", saved.CurrentStage);
        Assert.Empty(saved.Updates);
    }

    [WorkflowFact]
    public async Task A_failed_verification_marks_the_request_failed()
    {
        using var rig = new Rig();
        await rig.Seed(Request(), Type("Received"));
        rig.Verify(false, "unknown", message: "Reference not found");

        var result = await Run(rig, "SR_1");

        Assert.True(result.IsBadRequest);
        using var db = rig.Db();
        var saved = await db.ServiceRequests.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Failed", "Reference not found", ""), (saved.PaymentStatus, saved.FailureMessage, saved.CurrentStage));
    }

    [WorkflowTheory]
    [InlineData("failed")]
    [InlineData("abandoned")]
    public async Task A_non_success_status_fails_the_request_without_starting_it(string status)
    {
        using var rig = new Rig();
        await rig.Seed(Request(), Type("Received"));
        rig.Verify(true, status, 5000);

        var result = await Run(rig, "SR_1");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        var saved = await db.ServiceRequests.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Failed", $"Payment {status}.", ""), (saved.PaymentStatus, saved.FailureMessage, saved.CurrentStage));
        Assert.Empty(saved.Updates);
    }

    [WorkflowFact]
    public async Task The_webhook_body_is_kept_and_the_payment_channel_is_taken_from_it()
    {
        using var rig = new Rig();
        await rig.Seed(Request(), Type("Received"));
        rig.Verify(true, "success", 5000);
        var body = "{\"data\":{\"authorization\":{\"channel\":\"card\"}}}";

        await Run(rig, "SR_1", body);

        using var db = rig.Db();
        var saved = await db.ServiceRequests.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((body, "card"), (saved.CallbackPayload, saved.Channel));
    }

    [WorkflowFact]
    public async Task A_garbled_webhook_body_does_not_block_confirmation()
    {
        using var rig = new Rig();
        await rig.Seed(Request(), Type("Received"));
        rig.Verify(true, "success", 5000);

        await Run(rig, "SR_1", "<<not json>>");

        using var db = rig.Db();
        Assert.Equal("Successful", (await db.ServiceRequests.IgnoreQueryFilters().SingleAsync()).PaymentStatus);
    }
}
