using System.Text.Json;
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
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>One paid event, several RSVP prices ("Attendance only", "With certificate", ...): saving them, what locks once they sell, and what members see.</summary>
public class EventPriceOptionTests
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
            seed.Institutions.Add(new InstitutionEntity { Id = Tenant, Slug = "umat", Name = "UMaT", PaidEventsEnabled = paidEnabled, PaystackSubaccountCode = payout ? "ACCT_x" : null });
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

    private static string Json(params object[] options) => JsonSerializer.Serialize(options);
    private static object Opt(string name, decimal price, string? id = null, bool active = true, string? note = null) => new { id, name, description = note, price, isActive = active };

    private static CreateEventRequest NewEvent(string optionsJson) => new()
    {
        Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), IsTicketed = true, PriceOptionsJson = optionsJson,
    };

    private static UpdateEventRequest Edit(string id, string optionsJson, bool ticketed = true) => new()
    {
        EventId = id, Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), IsTicketed = ticketed, PriceOptionsJson = optionsJson, Status = "Upcoming",
    };

    private static AlumniEvent TwoPrices() => new()
    {
        Id = "e1", Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), Status = "Upcoming", IsTicketed = true, TicketPrice = 100m,
        PriceOptions = [new() { Id = "a", Name = "Attendance only", Price = 100m }, new() { Id = "c", Name = "With certificate", Price = 150m }],
    };

    private static EventRsvp Ticket(string member, string optionId, string optionName, decimal paid) => new()
    {
        Id = $"r-{member}", EventId = "e1", MemberId = member, Status = "Confirmed", AmountPaid = paid, PriceOptionId = optionId, PriceOptionName = optionName,
    };

    // ── Saving prices ───────────────────────────────────────────────────

    [Fact]
    public async Task An_event_can_have_several_prices_and_lists_the_lowest_as_its_starting_price()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(NewEvent(Json(Opt("With certificate", 150m, note: "Printed certificate"), Opt("Attendance only", 100m))), Admin);

        Assert.Equal(200 + 1, response.Code);   // created
        var saved = await rig.Db().Events.SingleAsync();
        Assert.Equal(100m, saved.TicketPrice);
        Assert.Equal(["With certificate", "Attendance only"], saved.PriceOptions.Select(o => o.Name));
        Assert.All(saved.PriceOptions, o => Assert.False(string.IsNullOrEmpty(o.Id)));
        Assert.Equal("Printed certificate", saved.PriceOptions[0].Description);
        Assert.Equal(2, response.Data!.PriceOptions.Count);
    }

    [Fact]
    public async Task Every_price_needs_a_name_a_price_and_a_different_name_from_the_others()
    {
        var rig = new Rig();
        Assert.Equal(400, (await rig.Service.CreateEventAsync(NewEvent(Json(Opt("", 100m))), Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateEventAsync(NewEvent(Json(Opt("Standard", 0m))), Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateEventAsync(NewEvent(Json(Opt("Standard", 100m), Opt("standard", 200m))), Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateEventAsync(NewEvent(Json(Opt("Standard", EventTicketRules.MaxTicketPrice + 1))), Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateEventAsync(NewEvent(Json()), Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateEventAsync(NewEvent("not json"), Admin)).Code);
        Assert.Empty(await rig.Db().Events.ToListAsync());
    }

    [Fact]
    public async Task There_is_a_limit_on_how_many_prices_one_event_can_have()
    {
        var rig = new Rig();
        var seven = Enumerable.Range(1, EventTicketRules.MaxPriceOptions + 1).Select(i => Opt($"Option {i}", 10m * i)).ToArray();
        Assert.Equal(400, (await rig.Service.CreateEventAsync(NewEvent(Json(seven)), Admin)).Code);
        Assert.Equal(201, (await rig.Service.CreateEventAsync(NewEvent(Json(seven.Take(EventTicketRules.MaxPriceOptions).ToArray())), Admin)).Code);
    }

    [Fact]
    public async Task At_least_one_price_must_be_on_sale()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(NewEvent(Json(Opt("Standard", 100m, active: false))), Admin);
        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task Prices_need_the_platform_s_go_ahead_and_a_payout_account_like_a_single_price_does()
    {
        Assert.Equal(EventTicketRules.PaidEventsOffMessage, (await new Rig(paidEnabled: false).Service.CreateEventAsync(NewEvent(Json(Opt("A", 10m), Opt("B", 20m))), Admin)).Message);
        Assert.Equal(EventTicketRules.PayoutMissingMessage, (await new Rig(payout: false).Service.CreateEventAsync(NewEvent(Json(Opt("A", 10m), Opt("B", 20m))), Admin)).Message);
    }

    // ── Changing prices after people bought ─────────────────────────────

    [Fact]
    public async Task A_price_people_bought_cannot_be_removed_but_can_be_switched_off()
    {
        var rig = new Rig();
        await rig.Seed(TwoPrices(), Ticket("m1", "c", "With certificate", 150m));

        var removed = await rig.Service.UpdateEventAsync(Edit("e1", Json(Opt("Attendance only", 100m, "a"))), Admin);
        Assert.Equal(400, removed.Code);
        Assert.Contains("With certificate", removed.Message);
        Assert.Equal(2, (await rig.Db().Events.SingleAsync()).PriceOptions.Count);

        var off = await rig.Service.UpdateEventAsync(Edit("e1", Json(Opt("Attendance only", 100m, "a"), Opt("With certificate", 150m, "c", active: false))), Admin);
        Assert.Equal(200, off.Code);
        var saved = await rig.Db().Events.SingleAsync();
        Assert.False(saved.PriceOptions.Single(o => o.Id == "c").IsActive);
        Assert.Equal(100m, saved.TicketPrice);
    }

    [Fact]
    public async Task A_price_nobody_bought_can_be_removed_and_a_new_one_added_after_sales_began()
    {
        var rig = new Rig();
        await rig.Seed(TwoPrices(), Ticket("m1", "a", "Attendance only", 100m));

        var response = await rig.Service.UpdateEventAsync(Edit("e1", Json(Opt("Attendance only", 100m, "a"), Opt("Student", 60m))), Admin);

        Assert.Equal(200, response.Code);
        var saved = await rig.Db().Events.SingleAsync();
        Assert.Equal(["Attendance only", "Student"], saved.PriceOptions.Select(o => o.Name));
        Assert.Equal("a", saved.PriceOptions[0].Id);          // the same option, so its tickets stay tied to it
        Assert.Equal(60m, saved.TicketPrice);
    }

    [Fact]
    public async Task Repricing_an_option_leaves_what_earlier_buyers_paid_alone()
    {
        var rig = new Rig();
        await rig.Seed(TwoPrices(), Ticket("m1", "c", "With certificate", 150m));

        await rig.Service.UpdateEventAsync(Edit("e1", Json(Opt("Attendance only", 100m, "a"), Opt("With certificate", 200m, "c"))), Admin);

        Assert.Equal(200m, (await rig.Db().Events.SingleAsync()).PriceOptions.Single(o => o.Id == "c").Price);
        Assert.Equal(150m, (await rig.Db().EventRsvps.SingleAsync()).AmountPaid);
    }

    [Fact]
    public async Task The_organiser_sees_how_many_bought_each_price()
    {
        var rig = new Rig();
        await rig.Seed(TwoPrices(), Ticket("m1", "c", "With certificate", 150m), Ticket("m2", "c", "With certificate", 150m), Ticket("m3", "a", "Attendance only", 100m));

        var listed = (await rig.Service.GetEventsAsync(new EventFilter(), Admin)).Data!.Results.Single();
        Assert.Equal([(1, "Attendance only"), (2, "With certificate")], listed.PriceOptions.OrderBy(o => o.Name).Select(o => (o.SoldCount!.Value, o.Name)));
        var one = (await rig.Service.GetEventByIdAsync("e1", Admin)).Data!;
        Assert.Equal(2, one.PriceOptions.Single(o => o.Id == "c").SoldCount);

        var rsvps = (await rig.Service.GetRsvpsAsync("e1", new BaseFilter(), Admin)).Data!.Results;
        Assert.Contains(rsvps, r => r.MemberId == "m1" && r.PriceOptionName == "With certificate");
    }

    // ── Events from before options ──────────────────────────────────────

    [Fact]
    public async Task An_event_priced_before_options_existed_shows_its_price_as_one_option_and_keeps_working()
    {
        var rig = new Rig();
        await rig.Seed(new AlumniEvent { Id = "e1", Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), Status = "Upcoming", IsTicketed = true, TicketPrice = 100m });

        var dto = (await rig.Service.GetEventByIdAsync("e1", Admin)).Data!;
        var only = Assert.Single(dto.PriceOptions);
        Assert.Equal((EventTicketRules.LegacyOptionId, "RSVP", 100m), (only.Id, only.Name, only.Price));

        // Saved again with just a price (an older form), it becomes a one-option event that keeps that id.
        Assert.Equal(200, (await rig.Service.UpdateEventAsync(new UpdateEventRequest { EventId = "e1", Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), IsTicketed = true, TicketPrice = 120m, Status = "Upcoming" }, Admin)).Code);
        var saved = await rig.Db().Events.SingleAsync();
        Assert.Equal((EventTicketRules.LegacyOptionId, 120m, 120m), (saved.PriceOptions.Single().Id, saved.PriceOptions.Single().Price, saved.TicketPrice!.Value));
    }

    [Fact]
    public async Task A_free_event_has_no_prices()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateEventAsync(new CreateEventRequest { Title = "Mixer", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(3) }, Admin);
        Assert.Equal(201, response.Code);
        var saved = await rig.Db().Events.SingleAsync();
        Assert.Empty(saved.PriceOptions);
        Assert.Null(saved.TicketPrice);
    }

    // ── What members see ────────────────────────────────────────────────

    [Fact]
    public void Members_see_only_the_prices_on_sale_and_start_from_the_lowest_of_those()
    {
        var ev = TwoPrices();
        ev.PriceOptions[0].IsActive = false;                    // "Attendance only" is switched off
        var dto = ev.ToDto().ForMembers();

        Assert.Equal(["With certificate"], dto.PriceOptions.Select(o => o.Name));
        Assert.Equal(150m, dto.TicketPrice);
        Assert.Equal(2, ev.ToDto().PriceOptions.Count);          // the organiser's own view still has both
    }
}
