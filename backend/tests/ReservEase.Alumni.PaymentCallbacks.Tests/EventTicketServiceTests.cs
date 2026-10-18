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
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.PaymentCallbacks.Tests;

public class EventTicketServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IPaystackService> Paystack { get; } = new();
        public Mock<ITemporalClientProvider> Temporal { get; } = new();
        public PaystackConfig PaystackConfig { get; } = new() { GatewayFeePercentage = 1.95m, GatewayFeeSafetyBufferSubunit = 2 };
        public List<InitializePaymentRequest> Initialized { get; } = new();
        public AuthData Me { get; } = new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };
        public EventTicketService Service { get; private set; } = null!;

        public Rig(bool paidEnabled = true, bool payout = true, bool paystackAccepts = true)
        {
            Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()))
                .Callback<InitializePaymentRequest>(r => Initialized.Add(r))
                .ReturnsAsync(() => new InitializePaymentResponse
                {
                    Status = paystackAccepts, Message = paystackAccepts ? "ok" : "Invalid key",
                    Data = new InitializePaymentData { AuthorizationUrl = "https://paystack/pay" },
                });
            Temporal.SetupGet(t => t.IsAvailable).Returns(false);

            using (var seed = TestDb.Create(DbName, Tenant))
            {
                seed.Institutions.Add(new Institution
                {
                    Id = Tenant, Slug = "umat", Name = "UMaT", PaidEventsEnabled = paidEnabled,
                    PaystackSubaccountCode = payout ? "ACCT_x" : null, PlatformFeePercentage = 3m,
                });
                seed.SaveChanges();
            }
            Fresh();
        }

        public EventTicketService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant, TestDb.Tenant(Tenant, "umat"));
            return Service = new EventTicketService(
                new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<Institution>(db),
                new AlumniPgRepository<CommunityMembership>(db), TestDb.Tenant(Tenant, "umat"), Paystack.Object, PaystackConfig, Temporal.Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PaystackConfig:CallbackUrl"] = "https://app.test/pay" }).Build(),
                NullLogger<EventTicketService>.Instance);
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

    private static AlumniEvent PaidEvent(string id = "e1", decimal price = 100m, int? capacity = null, string status = "Upcoming") => new()
    {
        Id = id, Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), Status = status, IsTicketed = true, TicketPrice = price, Capacity = capacity,
    };

    private static EventTicketRequest Buy(string eventId = "e1") => new() { EventId = eventId };

    // ── Starting a payment ──────────────────────────────────────────────

    [Fact]
    public async Task Buying_a_ticket_saves_a_pending_seat_and_charges_more_than_the_price_so_the_institution_gets_it_all()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(price: 100m));

        var response = await rig.Service.InitiateAsync(Buy(), rig.Me);

        Assert.Equal(200, response.Code);
        Assert.Equal("https://paystack/pay", response.Data!.AuthorizationUrl);
        Assert.StartsWith("ET_", response.Data.Reference);

        var rsvp = await rig.Db().EventRsvps.SingleAsync();
        Assert.Equal((EventRsvpStatuses.PendingPayment, 100m, response.Data.Reference), (rsvp.Status, rsvp.AmountPaid, rsvp.TransactionRef));
        Assert.True(rsvp.PlatformFeeAmount > 0);
        Assert.True(rsvp.GrossChargeAmount > 100m);

        var sent = Assert.Single(rig.Initialized);
        Assert.Equal("ACCT_x", sent.Subaccount);
        Assert.Equal((long)(rsvp.GrossChargeAmount * 100m), sent.Amount);   // what Paystack charges is the grossed-up amount
        Assert.Equal("true", sent.Metadata!["eventTicket"]);
    }

    [Fact]
    public async Task Tickets_are_refused_when_the_platform_has_not_allowed_paid_events()
    {
        var rig = new Rig(paidEnabled: false);
        await rig.Seed(PaidEvent());

        var response = await rig.Service.InitiateAsync(Buy(), rig.Me);

        Assert.Equal(400, response.Code);
        Assert.Contains("aren't on sale", response.Message);
        Assert.Empty(rig.Db().EventRsvps);
        rig.Paystack.Verify(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()), Times.Never);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task Tickets_are_refused_once_the_event_is_over_or_cancelled(string status)
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(status: status));
        Assert.Equal(400, (await rig.Service.InitiateAsync(Buy(), rig.Me)).Code);
    }

    [Fact]
    public async Task A_free_event_has_no_tickets()
    {
        var rig = new Rig();
        await rig.Seed(new AlumniEvent { Id = "e1", Title = "Picnic", Venue = "Park", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming" });
        var response = await rig.Service.InitiateAsync(Buy(), rig.Me);
        Assert.Equal(400, response.Code);
        Assert.Equal("This event doesn't sell tickets.", response.Message);
    }

    [Fact]
    public async Task An_unknown_event_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.InitiateAsync(Buy("nope"), rig.Me)).Code);
    }

    [Fact]
    public async Task Someone_who_already_holds_a_ticket_cannot_buy_another()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), new EventRsvp { Id = "r1", EventId = "e1", MemberId = "m1", Status = "Confirmed", AmountPaid = 100m });

        var response = await rig.Service.InitiateAsync(Buy(), rig.Me);

        Assert.Equal(409, response.Code);
        Assert.Equal(1, await rig.Db().EventRsvps.CountAsync());
    }

    [Fact]
    public async Task A_community_event_needs_an_approved_community_member()
    {
        var rig = new Rig();
        var ev = PaidEvent(); ev.CommunityId = "c1";
        await rig.Seed(ev, new CommunityMembership { Id = "cm", CommunityId = "c1", MemberId = "m1", Status = "Pending" });

        Assert.Equal(403, (await rig.Service.InitiateAsync(Buy(), rig.Me)).Code);
    }

    // ── Seats ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Seats_held_by_people_who_are_paying_right_now_count_toward_capacity()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(capacity: 2),
            new EventRsvp { Id = "r1", EventId = "e1", MemberId = "a", Status = "Confirmed", AmountPaid = 100m },
            new EventRsvp { Id = "r2", EventId = "e1", MemberId = "b", Status = "PendingPayment", AmountPaid = 100m });   // started just now

        var response = await rig.Service.InitiateAsync(Buy(), rig.Me);

        Assert.Equal(409, response.Code);
        Assert.Contains("full", response.Message);
    }

    [Fact]
    public async Task A_payment_that_was_abandoned_long_ago_no_longer_holds_its_seat()
    {
        var rig = new Rig();
        var stale = new EventRsvp { Id = "r2", EventId = "e1", MemberId = "b", Status = "PendingPayment", AmountPaid = 100m, CreatedAt = DateTime.UtcNow.AddHours(-3) };
        await rig.Seed(PaidEvent(capacity: 1), stale);

        Assert.Equal(200, (await rig.Service.InitiateAsync(Buy(), rig.Me)).Code);
    }

    [Fact]
    public async Task Failed_payments_do_not_hold_seats()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(capacity: 1), new EventRsvp { Id = "r2", EventId = "e1", MemberId = "b", Status = "PaymentFailed", AmountPaid = 100m });
        Assert.Equal(200, (await rig.Service.InitiateAsync(Buy(), rig.Me)).Code);
    }

    // ── Retrying ────────────────────────────────────────────────────────

    [Fact]
    public async Task Trying_again_after_a_failed_payment_reuses_the_same_row_with_a_new_reference()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), new EventRsvp { Id = "r1", EventId = "e1", MemberId = "m1", Status = "PaymentFailed", AmountPaid = 100m, TransactionRef = "ET_old", FailureMessage = "declined" });

        var response = await rig.Service.InitiateAsync(Buy(), rig.Me);

        Assert.Equal(200, response.Code);
        var rsvp = await rig.Db().EventRsvps.SingleAsync();
        Assert.Equal(("r1", EventRsvpStatuses.PendingPayment, response.Data!.Reference), (rsvp.Id, rsvp.Status, rsvp.TransactionRef));
        Assert.Null(rsvp.FailureMessage);
    }

    [Fact]
    public async Task If_paystack_refuses_to_start_the_payment_the_seat_is_not_left_held()
    {
        var rig = new Rig(paystackAccepts: false);
        await rig.Seed(PaidEvent());

        var response = await rig.Service.InitiateAsync(Buy(), rig.Me);

        Assert.Equal(400, response.Code);
        var rsvp = await rig.Db().EventRsvps.SingleAsync();
        Assert.Equal(EventRsvpStatuses.PaymentFailed, rsvp.Status);
    }

    // ── Status ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Confirmed", "Successful")]
    [InlineData("PendingPayment", "Pending")]
    [InlineData("PaymentFailed", "Failed")]
    public async Task The_status_endpoint_uses_the_same_words_as_the_store(string stored, string shown)
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), new EventRsvp { Id = "r1", EventId = "e1", MemberId = "m1", Status = stored, AmountPaid = 100m, TransactionRef = "ET_1" });

        var response = await rig.Service.GetStatusAsync("ET_1", rig.Me);

        Assert.Equal(200, response.Code);
        Assert.Equal((shown, "e1"), (response.Data!.Status, response.Data.EventId));
    }

    [Fact]
    public async Task Nobody_can_read_someone_elses_ticket_status()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), new EventRsvp { Id = "r1", EventId = "e1", MemberId = "other", Status = "Confirmed", AmountPaid = 100m, TransactionRef = "ET_1" });

        Assert.Equal(400, (await rig.Service.GetStatusAsync("ET_1", rig.Me)).Code);
        Assert.Equal(404, (await rig.Service.GetStatusAsync("ET_none", rig.Me)).Code);
    }

    [Fact]
    public async Task The_webhook_can_tell_which_references_are_tickets()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), new EventRsvp { Id = "r1", EventId = "e1", MemberId = "m1", Status = "PendingPayment", TransactionRef = "ET_1" });

        Assert.True(await rig.Service.OwnsReferenceAsync("ET_1"));
        Assert.False(await rig.Service.OwnsReferenceAsync("ET_2"));
    }
}
