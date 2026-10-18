using Microsoft.EntityFrameworkCore;
using Moq;
using ReservEase.Alumni.Operations.Worker.Workflows.EventTickets;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Temporalio.Client;
using Temporalio.Worker;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class EventTicketCallbackWorkflowTests(TemporalFixture temporal)
{
    private sealed class Rig : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
        public Mock<IPaystackService> Paystack { get; } = new();
        public CapturingLogger<EventTicketCallbackActivities> Log { get; } = new();
        public EventTicketCallbackActivities Activities { get; }

        public Rig()
        {
            var (first, conn) = TestDb.CreateRelational();
            first.Dispose();
            connection = conn;
            var ctx = TestDb.OpenRelational(connection);
            var provider = new Mock<ITemporalClientProvider>();
            provider.SetupGet(p => p.IsAvailable).Returns(false);   // the notification is then logged as dropped, which tests can see
            Activities = new EventTicketCallbackActivities(
                new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<AlumniEvent>(ctx), Paystack.Object, provider.Object, Log);
        }

        public AlumniDbContext Db() => TestDb.OpenRelational(connection);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) db.Add(e);
            await db.SaveChangesAsync();
        }

        public void Verify(bool ok, string status, long amountSubunit = 0, long? fees = null, string message = "Verification successful") =>
            Paystack.Setup(p => p.VerifyPaymentAsync(It.IsAny<string>())).ReturnsAsync(new VerifyPaymentResponse
            {
                Status = ok, Message = message,
                Data = new VerifyPaymentData { Status = status, Amount = amountSubunit, Fees = fees, GatewayResponse = "Approved" },
            });

        public bool NotificationWasDispatched => Log.Entries.Any(e => e.Message.Contains("EventRsvpConfirmed"));
        public void Dispose() => connection.Dispose();
    }

    private async Task<PaymentCallbackResult> Run(Rig rig, string reference, string? rawBody = null)
    {
        var queue = "q-" + Guid.NewGuid().ToString("N");
        using var worker = new TemporalWorker(temporal.Client,
            new TemporalWorkerOptions(queue).AddWorkflow<ProcessEventTicketCallbackWorkflow>().AddAllActivities(rig.Activities));
        return await worker.ExecuteAsync(() => temporal.Client.ExecuteWorkflowAsync(
            (ProcessEventTicketCallbackWorkflow wf) => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, rawBody!)),
            new WorkflowOptions("wf-" + Guid.NewGuid().ToString("N"), queue)));
    }

    private static AlumniEvent Event(int? capacity = null) =>
        new() { Id = "e1", Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(5), Status = "Upcoming", IsTicketed = true, TicketPrice = 100m, Capacity = capacity, InstitutionId = "i1" };

    private static EventRsvp Pending(string id = "r1", string member = "m1", string reference = "ET_1", string status = "PendingPayment") =>
        new() { Id = id, EventId = "e1", MemberId = member, Status = status, AmountPaid = 100m, TransactionRef = reference, InstitutionId = "i1" };

    [WorkflowFact]
    public async Task An_unknown_reference_is_rejected()
    {
        using var rig = new Rig();
        var result = await Run(rig, "ET_nope");
        Assert.True(result.IsBadRequest);
        Assert.Equal("Unknown payment reference.", result.Message);
        rig.Paystack.Verify(p => p.VerifyPaymentAsync(It.IsAny<string>()), Times.Never);
    }

    [WorkflowFact]
    public async Task A_paid_ticket_is_confirmed_counted_and_announced()
    {
        using var rig = new Rig();
        await rig.Seed(Event(), Pending(), Pending("r2", "m2", "ET_2", "Confirmed"));
        rig.Verify(true, "success", amountSubunit: 10450, fees: 250);

        var result = await Run(rig, "ET_1");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        var ticket = await db.EventRsvps.IgnoreQueryFilters().SingleAsync(r => r.Id == "r1");
        Assert.Equal(EventRsvpStatuses.Confirmed, ticket.Status);
        Assert.NotNull(ticket.PaidAt);
        Assert.Equal((104.50m, 2.50m, "Approved"), (ticket.GrossChargeAmount, ticket.GatewayFeeAmount, ticket.GatewayResponse));
        Assert.Equal(2, (await db.Events.IgnoreQueryFilters().SingleAsync()).RsvpCount);   // the other confirmed seat plus this one
        Assert.True(rig.NotificationWasDispatched);
    }

    [WorkflowFact]
    public async Task A_ticket_that_is_already_confirmed_is_not_verified_or_announced_again()
    {
        using var rig = new Rig();
        await rig.Seed(Event(), Pending(status: "Confirmed"));

        var result = await Run(rig, "ET_1");

        Assert.False(result.IsBadRequest);
        Assert.Contains("already verified", result.Message);
        rig.Paystack.Verify(p => p.VerifyPaymentAsync(It.IsAny<string>()), Times.Never);
        Assert.False(rig.NotificationWasDispatched);
    }

    [WorkflowFact]
    public async Task A_payment_paystack_says_did_not_succeed_fails_the_ticket_and_frees_the_seat()
    {
        using var rig = new Rig();
        await rig.Seed(Event(), Pending());
        rig.Verify(true, "abandoned", 10000);

        var result = await Run(rig, "ET_1");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        var ticket = await db.EventRsvps.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(EventRsvpStatuses.PaymentFailed, ticket.Status);
        Assert.Null(ticket.PaidAt);
        Assert.Equal(0, (await db.Events.IgnoreQueryFilters().SingleAsync()).RsvpCount);
        Assert.False(rig.NotificationWasDispatched);
    }

    [WorkflowFact]
    public async Task A_verification_error_fails_the_ticket()
    {
        using var rig = new Rig();
        await rig.Seed(Event(), Pending());
        rig.Verify(false, "unknown", message: "Transaction reference not found");

        var result = await Run(rig, "ET_1");

        Assert.True(result.IsBadRequest);
        using var db = rig.Db();
        var ticket = await db.EventRsvps.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((EventRsvpStatuses.PaymentFailed, "Transaction reference not found"), (ticket.Status, ticket.FailureMessage));
    }

    [WorkflowFact]
    public async Task A_paid_ticket_still_stands_when_the_event_filled_up_while_they_were_paying()
    {
        using var rig = new Rig();
        await rig.Seed(Event(capacity: 1), Pending("r2", "m2", "ET_2", "Confirmed"), Pending());
        rig.Verify(true, "success", 10000);

        await Run(rig, "ET_1");

        using var db = rig.Db();
        Assert.Equal(EventRsvpStatuses.Confirmed, (await db.EventRsvps.IgnoreQueryFilters().SingleAsync(r => r.Id == "r1")).Status);   // they paid, so they keep the seat
        Assert.Equal(2, (await db.Events.IgnoreQueryFilters().SingleAsync()).RsvpCount);   // over capacity by one, which is logged for the organiser to see
    }

    [WorkflowFact]
    public async Task The_channel_is_read_from_the_webhook_body()
    {
        using var rig = new Rig();
        await rig.Seed(Event(), Pending());
        rig.Verify(true, "success", 10000);

        await Run(rig, "ET_1", """{"data":{"authorization":{"channel":"mobile_money"}}}""");

        using var db = rig.Db();
        var ticket = await db.EventRsvps.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("mobile_money", ticket.Channel);
        Assert.Contains("mobile_money", ticket.CallbackPayload);
    }
}
