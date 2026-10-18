using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Paid events from the organiser's side: who may charge, what locks once tickets sell, and why a paid event can't be cancelled.</summary>
public class EventTicketingTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin1", Role = "SuperAdmin" };

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public EventService Service { get; private set; } = null!;

        public Rig(bool paidEnabled = true, bool payout = true)
        {
            using var seed = TestDb.Create(DbName, Tenant);
            seed.Institutions.Add(new InstitutionEntity
            {
                Id = Tenant, Slug = "umat", Name = "UMaT", PaidEventsEnabled = paidEnabled,
                PaystackSubaccountCode = payout ? "ACCT_x" : null,
            });
            seed.SaveChanges();
            Fresh();
        }

        public EventService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            var temporal = new Mock<ITemporalClientProvider>(); temporal.SetupGet(t => t.IsAvailable).Returns(false);
            return Service = new EventService(
                new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<MemberEntity>(db),
                new AlumniPgRepository<InstitutionEntity>(db), new AlumniPgRepository<EventUpdate>(db), Mock.Of<IStorageService>(), temporal.Object, TestDb.Tenant(Tenant, "umat"),
                Mock.Of<IRedisService<PublicContentCacheConfig>>(), Mock.Of<IInstitutionAuditLogService>(), NullLogger<EventService>.Instance);
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

    private static CreateEventRequest NewEvent(bool ticketed, decimal? price) => new()
    {
        Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), IsTicketed = ticketed, TicketPrice = price,
    };

    private static UpdateEventRequest Edit(string id, bool ticketed, decimal? price, string status = "Upcoming") => new()
    {
        EventId = id, Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), IsTicketed = ticketed, TicketPrice = price, Status = status,
    };

    private static AlumniEvent PaidEvent(string id = "e1", decimal price = 100m) => new()
    {
        Id = id, Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), Status = "Upcoming", IsTicketed = true, TicketPrice = price,
    };

    private static EventRsvp Ticket(string eventId, string memberId, decimal paid = 100m, string status = "Confirmed") => new()
    {
        Id = $"r-{eventId}-{memberId}", EventId = eventId, MemberId = memberId, Status = status, AmountPaid = paid,
    };

    // ── Creating ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_paid_event_is_refused_until_the_platform_turns_paid_events_on()
    {
        var rig = new Rig(paidEnabled: false);
        var response = await rig.Service.CreateEventAsync(NewEvent(true, 100m), Admin);
        Assert.Equal(400, response.Code);
        Assert.Equal(EventTicketRules.PaidEventsOffMessage, response.Message);
        Assert.Empty(rig.Db().Events);
    }

    [Fact]
    public async Task A_paid_event_is_refused_without_a_payout_account()
    {
        var rig = new Rig(payout: false);
        var response = await rig.Service.CreateEventAsync(NewEvent(true, 100m), Admin);
        Assert.Equal(400, response.Code);
        Assert.Equal(EventTicketRules.PayoutMissingMessage, response.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public async Task A_paid_event_needs_a_price(double? price)
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(NewEvent(true, price is null ? null : (decimal)price), Admin);
        Assert.Equal(400, response.Code);
        Assert.Equal("Enter the ticket price.", response.Message);
    }

    [Fact]
    public async Task A_paid_event_is_saved_with_its_price_when_everything_is_in_place()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(NewEvent(true, 150m), Admin);

        Assert.Equal(201, response.Code);
        var saved = await rig.Db().Events.SingleAsync();
        Assert.True(saved.IsTicketed);
        Assert.Equal(150m, saved.TicketPrice);
    }

    [Fact]
    public async Task A_free_event_ignores_any_price_and_needs_no_permission()
    {
        var rig = new Rig(paidEnabled: false, payout: false);
        var response = await rig.Service.CreateEventAsync(NewEvent(false, 99m), Admin);

        Assert.Equal(201, response.Code);
        var saved = await rig.Db().Events.SingleAsync();
        Assert.False(saved.IsTicketed);
        Assert.Null(saved.TicketPrice);
    }

    // ── A paid event can't be cancelled ─────────────────────────────────

    [Fact]
    public async Task A_paid_event_cannot_be_cancelled_even_before_any_ticket_is_sold()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent());

        var response = await rig.Service.CancelEventAsync("e1", Admin);

        Assert.Equal(400, response.Code);
        Assert.Equal(EventTicketRules.CannotCancelMessage, response.Message);
        Assert.Equal("Upcoming", (await rig.Db().Events.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_paid_event_cannot_be_cancelled_by_editing_its_status_either()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent());

        var response = await rig.Service.UpdateEventAsync(Edit("e1", true, 100m, "Cancelled"), Admin);

        Assert.Equal(400, response.Code);
        Assert.Equal(EventTicketRules.CannotCancelMessage, response.Message);
        Assert.Equal("Upcoming", (await rig.Db().Events.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_free_event_can_still_be_cancelled()
    {
        var rig = new Rig();
        await rig.Seed(new AlumniEvent { Id = "e1", Title = "Picnic", Venue = "Park", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming" });

        var response = await rig.Service.CancelEventAsync("e1", Admin);

        Assert.Equal(200, response.Code);
        Assert.Equal("Cancelled", (await rig.Db().Events.SingleAsync()).Status);
    }

    // ── Once tickets sell ───────────────────────────────────────────────

    [Fact]
    public async Task Details_can_change_after_tickets_sell_but_the_price_cannot()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), Ticket("e1", "m1"));

        var repriced = await rig.Service.UpdateEventAsync(Edit("e1", true, 80m), Admin);
        Assert.Equal(400, repriced.Code);
        Assert.Equal(EventTicketRules.PriceLockedMessage, repriced.Message);

        var rescheduled = await rig.Service.UpdateEventAsync(Edit("e1", true, 100m), Admin);
        Assert.Equal(200, rescheduled.Code);
        Assert.Equal(100m, (await rig.Db().Events.SingleAsync()).TicketPrice);
    }

    [Fact]
    public async Task A_paid_event_with_sales_cannot_become_free()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), Ticket("e1", "m1"));

        var response = await rig.Service.UpdateEventAsync(Edit("e1", false, null), Admin);

        Assert.Equal(400, response.Code);
        Assert.True((await rig.Db().Events.SingleAsync()).IsTicketed);
    }

    [Fact]
    public async Task Before_any_sale_the_price_can_change_and_the_event_can_go_back_to_free()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent());

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("e1", true, 120m), Admin)).Code);
        Assert.Equal(120m, (await rig.Db().Events.SingleAsync()).TicketPrice);

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("e1", false, null), Admin)).Code);
        Assert.False((await rig.Db().Events.SingleAsync()).IsTicketed);
    }

    [Fact]
    public async Task A_free_event_with_rsvps_cannot_turn_into_a_paid_one()
    {
        var rig = new Rig();
        await rig.Seed(new AlumniEvent { Id = "e1", Title = "Picnic", Venue = "Park", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming" },
            new EventRsvp { Id = "r1", EventId = "e1", MemberId = "m1", Status = "Confirmed" });

        var response = await rig.Service.UpdateEventAsync(Edit("e1", true, 100m), Admin);

        Assert.Equal(400, response.Code);
        Assert.Contains("already RSVP'd", response.Message);
    }

    [Fact]
    public async Task An_event_with_sold_tickets_cannot_be_deleted_but_one_without_can()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent("sold"), Ticket("sold", "m1"), PaidEvent("unsold"));

        var blocked = await rig.Service.DeleteEventAsync("sold", Admin);
        Assert.Equal(400, blocked.Code);
        Assert.Equal(EventTicketRules.CannotDeleteMessage, blocked.Message);

        Assert.Equal(200, (await rig.Service.DeleteEventAsync("unsold", Admin)).Code);
        Assert.Equal(new[] { "sold" }, rig.Db().Events.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task A_seat_at_a_paid_event_cannot_be_reopened_by_hand()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), new EventRsvp { Id = "r1", EventId = "e1", MemberId = "m1", Status = "PaymentFailed" });

        var response = await rig.Service.ReopenRsvpAsync("e1", "r1", Admin);

        Assert.Equal(400, response.Code);
        Assert.Equal("PaymentFailed", (await rig.Db().EventRsvps.SingleAsync()).Status);
    }

    // ── What the organiser sees ─────────────────────────────────────────

    [Fact]
    public async Task The_event_list_shows_tickets_sold_and_money_collected_without_any_fee()
    {
        var rig = new Rig();
        var ticket = Ticket("e1", "m1", 100m); ticket.PlatformFeeAmount = 3m; ticket.GatewayFeeAmount = 1.5m; ticket.GrossChargeAmount = 104.5m;
        await rig.Seed(PaidEvent(), ticket, Ticket("e1", "m2", 100m), Ticket("e1", "m3", 100m, "PendingPayment"));

        var page = (await rig.Service.GetEventsAsync(new EventFilter(), Admin)).Data!;
        var ev = page.Results.Single();

        Assert.Equal(2, ev.PaidTicketCount);                 // the pending one isn't a sale
        Assert.Equal(200m, ev.TicketAmountCollected);        // price times tickets, never grossed up
    }

    [Fact]
    public async Task The_form_is_told_whether_charging_is_allowed_and_payouts_are_ready()
    {
        Assert.Equal(new EventTicketingDto(true, true), (await new Rig().Service.GetTicketingAsync()).Data);
        Assert.Equal(new EventTicketingDto(false, true), (await new Rig(paidEnabled: false).Service.GetTicketingAsync()).Data);
        Assert.Equal(new EventTicketingDto(true, false), (await new Rig(payout: false).Service.GetTicketingAsync()).Data);
    }

    // ── The map or meeting link ─────────────────────────────────────────

    private static CreateEventRequest FreeEvent(string? link) => new() { Title = "Reunion", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), GoogleLocationUrl = link };

    [Theory]
    [InlineData("https://maps.app.goo.gl/abc123")]
    [InlineData("https://us02web.zoom.us/j/123456789?pwd=x")]
    [InlineData("https://meet.google.com/abc-defg-hij")]
    [InlineData("http://example.com/venue")]
    public async Task A_map_or_meeting_link_is_saved_as_given(string link)
    {
        var rig = new Rig();
        Assert.Equal(201, (await rig.Service.CreateEventAsync(FreeEvent(link), Admin)).Code);
        Assert.Equal(link, (await rig.Db().Events.SingleAsync()).GoogleLocationUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_link_means_none_is_stored_so_members_see_no_button(string? link)
    {
        var rig = new Rig();
        Assert.Equal(201, (await rig.Service.CreateEventAsync(FreeEvent(link), Admin)).Code);
        Assert.Null((await rig.Db().Events.SingleAsync()).GoogleLocationUrl);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("zoom.us/j/123")]
    [InlineData("not a link")]
    public async Task A_link_that_is_not_a_web_address_is_refused(string link)
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(FreeEvent(link), Admin);
        Assert.Equal(400, response.Code);
        Assert.Contains("web address", response.Message);
        Assert.Empty(rig.Db().Events);
    }

    [Fact]
    public async Task Editing_can_add_change_and_remove_the_link()
    {
        var rig = new Rig();
        await rig.Seed(new AlumniEvent { Id = "e1", Title = "Reunion", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming", GoogleLocationUrl = "https://maps.app.goo.gl/old" });
        UpdateEventRequest Edit(string? link) => new() { EventId = "e1", Title = "Reunion", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming", GoogleLocationUrl = link };

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("https://zoom.us/j/1"), Admin)).Code);
        Assert.Equal("https://zoom.us/j/1", (await rig.Db().Events.SingleAsync()).GoogleLocationUrl);

        Assert.Equal(400, (await rig.Service.UpdateEventAsync(Edit("javascript:alert(1)"), Admin)).Code);
        Assert.Equal("https://zoom.us/j/1", (await rig.Db().Events.SingleAsync()).GoogleLocationUrl);   // a refused edit changes nothing

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit(""), Admin)).Code);
        Assert.Null((await rig.Db().Events.SingleAsync()).GoogleLocationUrl);
    }
}
