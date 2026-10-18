using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>What a member sees of an event's updates: everything if they have a seat, only that updates exist if they don't.</summary>
public class MemberEventUpdateServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Me = new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public MemberEventUpdateService Service { get; private set; } = null!;
        public Rig() => Fresh();

        public MemberEventUpdateService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new MemberEventUpdateService(
                new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventUpdate>(db), new AlumniPgRepository<EventRsvp>(db),
                new AlumniPgRepository<CommunityMembership>(db), NullLogger<MemberEventUpdateService>.Instance);
        }

        public async Task Seed(params object[] entities)
        {
            using var db = TestDb.Create(DbName, Tenant);
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }
    }

    private static AlumniEvent Event(string? community = null) => new() { Id = "e1", Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(5), Status = "Upcoming", CommunityId = community };

    private static EventUpdate Update(string id, string category, bool pinned = false, int ageDays = 0) => new()
    {
        Id = id, EventId = "e1", Title = $"Update {id}", Category = category, IsPinned = pinned, CreatedAt = DateTime.UtcNow.AddDays(-ageDays), PostedByName = "Kojo Mensah",
        Blocks =
        [
            new EventUpdateBlock { Id = "t", Type = "Text", Text = "Secret schedule details" },
            new EventUpdateBlock { Id = "i", Type = "Images", Items = [new EventUpdateItem { Url = "https://cdn/private.jpg", Name = "private.jpg" }] },
        ],
    };

    private static EventRsvp Seat(string status = "Confirmed", decimal? paid = null) => new() { Id = "r1", EventId = "e1", MemberId = "m1", Status = status, AmountPaid = paid };

    // ── People with a seat ──────────────────────────────────────────────

    [Fact]
    public async Task Someone_with_an_rsvp_gets_every_update_with_all_its_parts()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Seat(), Update("a", "Schedule"), Update("b", "Recap", ageDays: 1));

        var r = (await rig.Service.GetAsync("e1", Me)).Data!;

        Assert.False(r.Locked);
        Assert.Equal(2, r.Count);
        Assert.Equal(2, r.Updates.Count);
        var first = r.Updates[0];
        Assert.Equal(new[] { "Text", "Images" }, first.Blocks.Select(b => b.Type));
        Assert.Equal("Secret schedule details", first.Blocks[0].Text);
        Assert.Equal("https://cdn/private.jpg", first.Blocks[1].Items[0].Url);
    }

    [Fact]
    public async Task Someone_who_bought_a_ticket_has_a_seat_too()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Seat(paid: 150m), Update("a", "Schedule"));
        Assert.False((await rig.Service.GetAsync("e1", Me)).Data!.Locked);
    }

    [Fact]
    public async Task Pinned_updates_lead_then_newest_first()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Seat(), Update("old", "Recap", ageDays: 5), Update("new", "Recap"), Update("pinned", "Recap", pinned: true, ageDays: 20));
        Assert.Equal(new[] { "pinned", "new", "old" }, (await rig.Service.GetAsync("e1", Me)).Data!.Updates.Select(u => u.Id));
    }

    [Fact]
    public async Task Members_never_see_which_staff_member_posted()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Seat(), Update("a", "Recap"));
        Assert.Null((await rig.Service.GetAsync("e1", Me)).Data!.Updates.Single().PostedByName);
    }

    // ── Everyone else: a teaser, never content ──────────────────────────

    [Theory]
    [InlineData(null)]                // no RSVP at all
    [InlineData("Cancelled")]         // gave up their seat
    [InlineData("PendingPayment")]    // still paying
    [InlineData("PaymentFailed")]
    public async Task Without_a_seat_the_updates_are_locked_and_no_content_is_returned(string? status)
    {
        var rig = new Rig();
        var seeds = new List<object> { Event(), Update("a", "Schedule"), Update("b", "Schedule"), Update("c", "Photos & video") };
        if (status is not null) seeds.Add(Seat(status));
        await rig.Seed([.. seeds]);

        var r = (await rig.Service.GetAsync("e1", Me)).Data!;

        Assert.True(r.Locked);
        Assert.Equal(3, r.Count);                       // they can see that updates exist
        Assert.Empty(r.Updates);                        // and nothing of what is in them
        Assert.Equal(new[] { ("Schedule", 2), ("Photos & video", 1) }, r.Categories.Select(c => (c.Category, c.Count)));
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        Assert.DoesNotContain("Secret schedule details", json);
        Assert.DoesNotContain("private.jpg", json);
        Assert.DoesNotContain("Update a", json);        // not even the titles
    }

    [Fact]
    public async Task An_event_with_no_updates_returns_an_empty_locked_or_open_list()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        var r = (await rig.Service.GetAsync("e1", Me)).Data!;
        Assert.Equal((true, 0), (r.Locked, r.Count));
        Assert.Empty(r.Categories);
    }

    [Fact]
    public async Task Another_members_seat_does_not_unlock_mine()
    {
        var rig = new Rig();
        await rig.Seed(Event(), new EventRsvp { Id = "r9", EventId = "e1", MemberId = "someone-else", Status = "Confirmed" }, Update("a", "Recap"));
        Assert.True((await rig.Service.GetAsync("e1", Me)).Data!.Locked);
    }

    [Fact]
    public async Task Updates_of_a_different_event_are_not_mixed_in()
    {
        var rig = new Rig();
        var other = Update("x", "Recap"); other.EventId = "e2";
        await rig.Seed(Event(), Seat(), Update("a", "Recap"), other);
        Assert.Equal(new[] { "a" }, (await rig.Service.GetAsync("e1", Me)).Data!.Updates.Select(u => u.Id));
    }

    // ── Community events and unknown events ─────────────────────────────

    [Fact]
    public async Task A_community_events_updates_need_an_approved_community_member()
    {
        var rig = new Rig();
        await rig.Seed(Event("c1"), Seat(), Update("a", "Recap"), new CommunityMembership { Id = "cm", CommunityId = "c1", MemberId = "m1", Status = "Pending" });
        Assert.Equal(403, (await rig.Service.GetAsync("e1", Me)).Code);
    }

    [Fact]
    public async Task An_approved_community_member_with_a_seat_sees_them()
    {
        var rig = new Rig();
        await rig.Seed(Event("c1"), Seat(), Update("a", "Recap"), new CommunityMembership { Id = "cm", CommunityId = "c1", MemberId = "m1", Status = "Approved" });
        Assert.False((await rig.Service.GetAsync("e1", Me)).Data!.Locked);
    }

    [Fact]
    public async Task An_unknown_event_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.GetAsync("nope", Me)).Code);
    }
}
