using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Text;
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
    public async Task The_price_can_change_after_tickets_sell_and_earlier_buyers_keep_what_they_paid()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(price: 100m), Ticket("e1", "m1", 100m));

        var repriced = await rig.Service.UpdateEventAsync(Edit("e1", true, 80m), Admin);

        Assert.Equal(200, repriced.Code);
        Assert.Equal(80m, (await rig.Db().Events.SingleAsync()).TicketPrice);                      // new buyers pay this
        Assert.Equal(100m, (await rig.Db().EventRsvps.SingleAsync()).AmountPaid);                  // the sold ticket still says what was paid
        var listed = (await rig.Service.GetEventsAsync(new EventFilter(), Admin)).Data!.Results.Single();
        Assert.Equal(100m, listed.TicketAmountCollected);                                          // collected money is what was actually paid, not tickets x new price
    }

    [Fact]
    public async Task The_price_can_go_up_as_well_as_down_and_details_change_too()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(price: 100m), Ticket("e1", "m1", 100m), Ticket("e1", "m2", 100m));

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("e1", true, 150m), Admin)).Code);
        Assert.Equal(150m, (await rig.Db().Events.SingleAsync()).TicketPrice);
        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("e1", true, 150m), Admin)).Code);   // same price, other details: also fine
    }

    [Fact]
    public async Task Repricing_still_needs_a_positive_price_and_stays_within_the_limit()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), Ticket("e1", "m1"));

        Assert.Equal("Enter the ticket price.", (await rig.Service.UpdateEventAsync(Edit("e1", true, 0m), Admin)).Message);
        Assert.Equal(400, (await rig.Service.UpdateEventAsync(Edit("e1", true, EventTicketRules.MaxTicketPrice + 1), Admin)).Code);
        Assert.Equal(100m, (await rig.Db().Events.SingleAsync()).TicketPrice);
    }

    [Fact]
    public async Task A_paid_event_with_sales_cannot_become_free()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), Ticket("e1", "m1"));

        var response = await rig.Service.UpdateEventAsync(Edit("e1", false, null), Admin);

        Assert.Equal(400, response.Code);
        Assert.Equal(EventTicketRules.CannotBecomeFreeMessage, response.Message);
        Assert.True((await rig.Db().Events.SingleAsync()).IsTicketed);
    }

    [Fact]
    public async Task Before_any_sale_the_event_can_also_go_back_to_free()
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

    // ── Closing and reopening RSVPs ─────────────────────────────────────

    [Theory]
    [InlineData("Upcoming")]
    [InlineData("Ongoing")]
    public async Task An_organiser_can_close_and_reopen_rsvps_on_a_live_event(string status)
    {
        var rig = new Rig();
        var ev = PaidEvent(); ev.IsTicketed = false; ev.TicketPrice = null; ev.Status = status;
        await rig.Seed(ev, Ticket("e1", "m1", 0m));

        var closed = await rig.Service.SetRegistrationClosedAsync("e1", true, Admin);
        Assert.Equal(200, closed.Code);
        Assert.True(closed.Data!.RegistrationClosed);
        Assert.Equal(1, closed.Data.RsvpCount);                              // seats already taken are untouched
        Assert.True((await rig.Db().Events.SingleAsync()).RegistrationClosed);
        Assert.Equal(1, await rig.Db().EventRsvps.CountAsync());

        var reopened = await rig.Service.SetRegistrationClosedAsync("e1", false, Admin);
        Assert.False(reopened.Data!.RegistrationClosed);
        Assert.False((await rig.Db().Events.SingleAsync()).RegistrationClosed);
    }

    [Fact]
    public async Task A_paid_event_can_stop_selling_even_though_it_cannot_be_cancelled()
    {
        var rig = new Rig();
        await rig.Seed(PaidEvent(), Ticket("e1", "m1"));

        Assert.Equal(400, (await rig.Service.CancelEventAsync("e1", Admin)).Code);             // can't cancel
        Assert.Equal(200, (await rig.Service.SetRegistrationClosedAsync("e1", true, Admin)).Code);   // but can stop sales

        var ev = await rig.Db().Events.SingleAsync();
        Assert.Equal(("Upcoming", true, 100m), (ev.Status, ev.RegistrationClosed, ev.TicketPrice));   // still on, still priced
        Assert.Equal("Confirmed", (await rig.Db().EventRsvps.SingleAsync()).Status);                   // the sold ticket stands
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task An_event_that_is_over_has_nothing_to_close(string status)
    {
        var rig = new Rig();
        var ev = PaidEvent(); ev.IsTicketed = false; ev.TicketPrice = null; ev.Status = status;
        await rig.Seed(ev);
        Assert.Equal(400, (await rig.Service.SetRegistrationClosedAsync("e1", true, Admin)).Code);
        Assert.False((await rig.Db().Events.SingleAsync()).RegistrationClosed);
    }

    [Fact]
    public async Task Editing_an_event_does_not_reopen_a_closed_one()
    {
        var rig = new Rig();
        var ev = PaidEvent(); ev.IsTicketed = false; ev.TicketPrice = null; ev.RegistrationClosed = true;
        await rig.Seed(ev);

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(new UpdateEventRequest { EventId = "e1", Title = "New title", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming" }, Admin)).Code);

        Assert.True((await rig.Db().Events.SingleAsync()).RegistrationClosed);
    }

    [Fact]
    public async Task A_scoped_admin_cannot_close_an_event_outside_their_scope_and_an_unknown_event_is_404()
    {
        var rig = new Rig();
        var ev = PaidEvent(); ev.IsTicketed = false; ev.TicketPrice = null; ev.YearGroups = [2010];
        await rig.Seed(ev);
        var scoped = new AuthData { Id = "scoped", Role = "ScopedAdmin", YearGroups = [2020] };

        Assert.Equal(404, (await rig.Service.SetRegistrationClosedAsync("e1", true, scoped)).Code);
        Assert.Equal(404, (await rig.Service.SetRegistrationClosedAsync("nope", true, Admin)).Code);
        Assert.False((await rig.Db().Events.SingleAsync()).RegistrationClosed);
    }

    // ── Rich-text descriptions ──────────────────────────────────────────

    private static CreateEventRequest WithDescription(string? description) =>
        new() { Title = "Reunion", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), Description = description };

    [Fact]
    public async Task A_formatted_description_is_saved_with_its_formatting()
    {
        var rig = new Rig();
        var html = "<h2>Programme</h2><p>Doors open at <strong>5pm</strong>.</p><ul><li>Tea</li><li>Talks</li></ul>";
        Assert.Equal(201, (await rig.Service.CreateEventAsync(WithDescription(html), Admin)).Code);
        Assert.Equal(html, (await rig.Db().Events.SingleAsync()).Description);
    }

    [Fact]
    public async Task Anything_that_could_run_code_is_removed_before_a_description_is_saved()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(WithDescription("<p>Hello</p><script>alert(1)</script><img src=x onerror=alert(2)><a href=\"javascript:alert(3)\">go</a>"), Admin);

        Assert.Equal(201, response.Code);
        var saved = (await rig.Db().Events.SingleAsync()).Description!;
        Assert.Contains("Hello", saved);
        foreach (var bad in new[] { "script", "onerror", "javascript", "<img" }) Assert.DoesNotContain(bad, saved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", response.Data!.Description ?? "", StringComparison.OrdinalIgnoreCase);   // and the answer sent back is the cleaned one
    }

    [Fact]
    public async Task A_plain_text_description_becomes_paragraphs()
    {
        var rig = new Rig();
        await rig.Service.CreateEventAsync(WithDescription("Bring a friend.\nBring a dish.\n\nSee you there!"), Admin);
        Assert.Equal("<p>Bring a friend.<br>Bring a dish.</p><p>See you there!</p>", (await rig.Db().Events.SingleAsync()).Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p></p>")]
    [InlineData("   ")]
    public async Task An_empty_editor_saves_no_description(string? description)
    {
        var rig = new Rig();
        await rig.Service.CreateEventAsync(WithDescription(description), Admin);
        Assert.Null((await rig.Db().Events.SingleAsync()).Description);
    }

    [Fact]
    public async Task A_huge_description_is_refused_rather_than_cut()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(WithDescription("<p>" + new string('x', RichText.MaxPlainLength + 1) + "</p>"), Admin);
        Assert.Equal(400, response.Code);
        Assert.Equal(RichText.TooLongMessage, response.Message);
        Assert.Empty(rig.Db().Events);
    }

    [Fact]
    public async Task Editing_cleans_the_description_too_and_can_clear_it()
    {
        var rig = new Rig();
        await rig.Seed(new AlumniEvent { Id = "e1", Title = "Reunion", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming", Description = "old words" });
        UpdateEventRequest Edit(string? d) => new() { EventId = "e1", Title = "Reunion", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(3), Status = "Upcoming", Description = d };

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("<p onmouseover=\"x()\">new <em>words</em></p>"), Admin)).Code);
        Assert.Equal("<p>new <em>words</em></p>", (await rig.Db().Events.SingleAsync()).Description);

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("<p></p>"), Admin)).Code);
        Assert.Null((await rig.Db().Events.SingleAsync()).Description);
    }
}
