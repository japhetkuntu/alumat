using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class MemberEventServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<ITemporalClientProvider> Temporal { get; } = new();
        public AuthData Me { get; } = new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };
        public MemberEventService Service { get; private set; } = null!;
        public Rig() { Temporal.SetupGet(t => t.IsAvailable).Returns(false); Fresh(); }

        public MemberEventService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new MemberEventService(
                new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<MemberEntity>(db),
                new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<Community>(db), Temporal.Object, NullLogger<MemberEventService>.Instance);
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

    private static AlumniEvent Event(string id = "e1", string status = "Upcoming", int? capacity = null, string? community = null, List<int>? years = null) =>
        new() { Id = id, Title = $"Event {id}", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(10), Status = status, Capacity = capacity, CommunityId = community, YearGroups = years };

    private static MemberEntity Person(int year = 2015) => new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com", GraduationYear = year };

    private static EventRsvp Rsvp(string eventId, string memberId, string status = "Confirmed") =>
        new() { Id = $"r-{eventId}-{memberId}", EventId = eventId, MemberId = memberId, Status = status };

    // ── Listing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task The_default_list_is_upcoming_events_soonest_first_with_live_rsvp_counts()
    {
        var rig = new Rig();
        var later = Event("later"); later.StartDate = DateTime.UtcNow.AddDays(20);
        await rig.Seed(Person(), later, Event("soon"), Event("done", "Completed"), Event("off", "Cancelled"),
            Rsvp("soon", "a"), Rsvp("soon", "b"), Rsvp("soon", "c", "Cancelled"));

        var page = (await rig.Service.GetEventsAsync(new EventFilter(), "m1")).Data!;

        Assert.Equal(new[] { "soon", "later" }, page.Results.Select(e => e.Id));
        Assert.Equal((2, 0), (page.Results.First().RsvpCount, page.Results.Last().RsvpCount));   // cancelled RSVPs don't count
    }

    [Fact]
    public async Task A_status_filter_selects_that_status_and_all_includes_cancelled_events()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Event("up"), Event("done", "Completed"), Event("off", "Cancelled"));

        Assert.Equal(new[] { "done" }, (await rig.Service.GetEventsAsync(new EventFilter { Status = "Completed" }, "m1")).Data!.Results.Select(e => e.Id));
        Assert.Equal(3, (await rig.Service.GetEventsAsync(new EventFilter { Status = "all" }, "m1")).Data!.TotalCount);
    }

    [Fact]
    public async Task Year_group_events_are_shown_only_to_members_of_those_years()
    {
        var rig = new Rig();
        await rig.Seed(Person(2015), Event("open"), Event("mine", years: [2015]), Event("other", years: [2020]), Event("empty-list", years: []));

        var ids = (await rig.Service.GetEventsAsync(new EventFilter(), "m1")).Data!.Results.Select(e => e.Id).OrderBy(x => x);

        Assert.Equal(new[] { "empty-list", "mine", "open" }, ids);
    }

    [Fact]
    public async Task Community_events_are_hidden_until_the_member_is_approved_into_the_community()
    {
        var rig = new Rig();
        await rig.Seed(Person(), new Community { Id = "c1", Name = "Engineers" }, Event("public"), Event("private", community: "c1"));

        Assert.Equal(new[] { "public" }, (await rig.Service.GetEventsAsync(new EventFilter(), "m1")).Data!.Results.Select(e => e.Id));

        await rig.Seed(new CommunityMembership { Id = "cm", CommunityId = "c1", MemberId = "m1", Status = "Approved" });
        var ids = (await rig.Service.GetEventsAsync(new EventFilter(), "m1")).Data!.Results.ToDictionary(e => e.Id);
        Assert.Equal(2, ids.Count);
        Assert.Equal("Engineers", ids["private"].CommunityName);
    }

    [Fact]
    public async Task A_pending_community_membership_does_not_unlock_its_events()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Event("private", community: "c1"), new CommunityMembership { Id = "cm", CommunityId = "c1", MemberId = "m1", Status = "Pending" });
        Assert.Empty((await rig.Service.GetEventsAsync(new EventFilter(), "m1")).Data!.Results);
    }

    [Fact]
    public async Task Asking_for_one_communitys_events_requires_approved_membership_of_it()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Event("private", community: "c1"));

        var denied = await rig.Service.GetEventsAsync(new EventFilter { CommunityId = "c1" }, "m1");
        Assert.Equal(403, denied.Code);

        await rig.Seed(new CommunityMembership { Id = "cm", CommunityId = "c1", MemberId = "m1", Status = "Approved" });
        var allowed = await rig.Service.GetEventsAsync(new EventFilter { CommunityId = "c1" }, "m1");
        Assert.Equal(new[] { "private" }, allowed.Data!.Results.Select(e => e.Id));
    }

    // ── Detail ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Event_detail_recalculates_the_rsvp_count_and_404s_for_unknown_events()
    {
        var rig = new Rig();
        var ev = Event(); ev.RsvpCount = 99;   // stale stored value
        await rig.Seed(ev, Rsvp("e1", "a"), Rsvp("e1", "b", "Cancelled"));

        Assert.Equal(1, (await rig.Service.GetEventByIdAsync("e1")).Data!.RsvpCount);
        Assert.Equal(404, (await rig.Service.GetEventByIdAsync("ghost")).Code);
    }

    // ── RSVP ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_first_rsvp_is_created_confirmed_and_the_count_updated()
    {
        var rig = new Rig();
        await rig.Seed(Event());

        var response = await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me);

        Assert.Equal(201, response.Code);
        var rsvp = await rig.Db().EventRsvps.SingleAsync();
        Assert.Equal(("Confirmed", "m1", "m1"), (rsvp.Status, rsvp.MemberId, rsvp.CreatedBy));   // the event/member snapshots are intentionally not persisted
        Assert.Equal(1, (await rig.Db().Events.SingleAsync()).RsvpCount);
    }

    [Fact]
    public async Task Rsvping_twice_is_a_conflict_and_does_not_double_count()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me);
        rig.Fresh();

        var again = await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me);

        Assert.Equal(409, again.Code);
        Assert.Equal(1, await rig.Db().EventRsvps.CountAsync());
        Assert.Equal(1, (await rig.Db().Events.SingleAsync()).RsvpCount);
    }

    [Fact]
    public async Task A_cancelled_rsvp_can_be_re_confirmed_and_counts_again()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Rsvp("e1", "m1", "Cancelled"));

        var response = await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me);

        Assert.Equal(200, response.Code);
        Assert.Equal("RSVP re-confirmed", response.Message);
        Assert.Equal("Confirmed", (await rig.Db().EventRsvps.SingleAsync()).Status);
        Assert.Equal(1, (await rig.Db().Events.SingleAsync()).RsvpCount);
    }

    [Fact]
    public async Task Rsvp_to_an_unknown_event_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.RsvpAsync(new RsvpRequest("ghost"), rig.Me)).Code);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task A_cancelled_or_finished_event_takes_no_new_rsvps(string status)
    {
        var rig = new Rig();
        await rig.Seed(Event(status: status));

        var response = await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me);

        Assert.Equal(400, response.Code);
        Assert.Contains("no longer open", response.Message);
        Assert.Equal(0, await rig.Db().EventRsvps.CountAsync());
    }

    [Fact]
    public async Task A_cancelled_rsvp_cannot_be_revived_on_a_cancelled_event()
    {
        var rig = new Rig();
        await rig.Seed(Event(status: "Cancelled"), Rsvp("e1", "m1", "Cancelled"));
        Assert.Equal(400, (await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me)).Code);
        Assert.Equal("Cancelled", (await rig.Db().EventRsvps.SingleAsync()).Status);
    }

    [Fact]
    public async Task An_event_with_a_capacity_stops_taking_rsvps_once_full()
    {
        var rig = new Rig();
        await rig.Seed(Event(capacity: 2), Rsvp("e1", "a"), Rsvp("e1", "b"));

        var response = await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me);

        Assert.Equal(409, response.Code);
        Assert.Contains("full", response.Message);
        Assert.Equal(2, await rig.Db().EventRsvps.CountAsync());
    }

    [Fact]
    public async Task Cancelled_rsvps_free_up_places_and_one_place_left_is_enough()
    {
        var rig = new Rig();
        await rig.Seed(Event(capacity: 2), Rsvp("e1", "a"), Rsvp("e1", "b", "Cancelled"));
        Assert.Equal(201, (await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me)).Code);
    }

    [Fact]
    public async Task A_full_event_also_refuses_to_re_confirm_a_cancelled_rsvp()
    {
        var rig = new Rig();
        await rig.Seed(Event(capacity: 1), Rsvp("e1", "a"), Rsvp("e1", "m1", "Cancelled"));
        Assert.Equal(409, (await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me)).Code);
    }

    [Fact]
    public async Task An_event_without_a_capacity_is_never_full()
    {
        var rig = new Rig();
        var seed = new List<object> { Event(capacity: null) };
        seed.AddRange(Enumerable.Range(0, 200).Select(i => (object)Rsvp("e1", $"u{i}")));
        await rig.Seed(seed.ToArray());
        Assert.Equal(201, (await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me)).Code);
    }

    [Fact]
    public async Task A_community_event_needs_approved_membership_to_rsvp()
    {
        var rig = new Rig();
        await rig.Seed(Event(community: "c1"));
        Assert.Equal(403, (await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me)).Code);

        await rig.Seed(new CommunityMembership { Id = "cm", CommunityId = "c1", MemberId = "m1", Status = "Approved" });
        Assert.Equal(201, (await rig.Service.RsvpAsync(new RsvpRequest("e1"), rig.Me)).Code);
    }

    // ── Cancel ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_an_rsvp_marks_it_cancelled_and_lowers_the_count()
    {
        var rig = new Rig();
        var ev = Event(); ev.RsvpCount = 2;
        await rig.Seed(ev, Rsvp("e1", "m1"), Rsvp("e1", "other"));

        var response = await rig.Service.CancelRsvpAsync("e1", rig.Me);

        Assert.Equal(200, response.Code);
        var rsvp = await rig.Db().EventRsvps.SingleAsync(r => r.MemberId == "m1");
        Assert.Equal("Cancelled", rsvp.Status);
        Assert.NotNull(rsvp.UpdatedAt);
        Assert.Equal(1, (await rig.Db().Events.SingleAsync()).RsvpCount);
    }

    [Fact]
    public async Task Cancelling_when_there_is_no_confirmed_rsvp_is_404()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Rsvp("e1", "m1", "Cancelled"));
        Assert.Equal(404, (await rig.Service.CancelRsvpAsync("e1", rig.Me)).Code);
        Assert.Equal(404, (await rig.Service.CancelRsvpAsync("ghost", rig.Me)).Code);
    }

    // ── My RSVPs ────────────────────────────────────────────────────────

    [Fact]
    public async Task My_rsvps_default_to_confirmed_and_can_be_filtered_or_widened()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Event("e1"), Event("e2"), Event("e3"),
            Rsvp("e1", "m1"), Rsvp("e2", "m1", "Cancelled"), Rsvp("e3", "other"));

        Assert.Equal(new[] { "e1" }, (await rig.Service.GetMyRsvpsAsync("m1")).Data!.Select(r => r.EventId));
        Assert.Equal(new[] { "e2" }, (await rig.Service.GetMyRsvpsAsync("m1", "Cancelled")).Data!.Select(r => r.EventId));
        Assert.Equal(2, (await rig.Service.GetMyRsvpsAsync("m1", "ALL")).Data!.Count());
    }

    [Fact]
    public async Task Rsvps_are_returned_with_the_event_details_and_the_live_member_name()
    {
        var rig = new Rig();
        var person = Person(); person.ProfilePictureUrl = "https://pic";
        await rig.Seed(person, Event("e1"), Rsvp("e1", "m1"));

        var dto = (await rig.Service.GetMyRsvpsAsync("m1")).Data!.Single();

        Assert.Equal(("Ama Mensah", "https://pic"), (dto.MemberName, dto.MemberProfilePictureUrl));
        Assert.Equal("Event e1", dto.EventTitle);
    }
}
