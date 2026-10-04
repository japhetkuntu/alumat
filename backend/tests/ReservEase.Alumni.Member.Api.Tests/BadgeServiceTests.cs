using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class BadgeServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly int ThisYear = DateTime.UtcNow.Year;

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public BadgeService Service { get; private set; } = null!;
        public Rig() => Fresh();

        public BadgeService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new BadgeService(
                new AlumniPgRepository<MemberBadge>(db), new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Contribution>(db),
                new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<Referral>(db),
                NullLogger<BadgeService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }

        public async Task<List<string>> Badges(string memberId = "m1") =>
            (await Db().MemberBadges.Where(b => b.MemberId == memberId).Select(b => b.BadgeType).ToListAsync()).OrderBy(x => x).ToList();
    }

    private static MemberEntity Person(string id = "m1") => new() { Id = id, FirstName = "Ama", LastName = "Mensah", Email = $"{id}@x.com" };

    private static Contribution Paid(string campaignId, string memberId = "m1") =>
        new() { Id = $"c-{campaignId}-{memberId}", CampaignId = campaignId, MemberId = memberId, Status = "Successful" };

    private static Campaign Dues(int year) => new() { Id = $"dues-{year}", Title = $"Dues {year}", IsMembershipCampaign = true, MembershipYear = year, Deadline = DateTime.UtcNow.AddDays(30) };

    [Fact]
    public async Task A_member_with_no_activity_earns_nothing()
    {
        var rig = new Rig();
        await rig.Seed(Person());
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Empty(await rig.Badges());
    }

    [Fact]
    public async Task An_unknown_member_is_ignored_without_error()
    {
        var rig = new Rig();
        await rig.Service.EvaluateAndAwardBadgesAsync("ghost");
        Assert.Equal(0, await rig.Db().MemberBadges.CountAsync());
    }

    [Fact]
    public async Task The_first_successful_contribution_earns_the_first_contribution_badge_but_a_pending_one_does_not()
    {
        var rig = new Rig();
        await rig.Seed(Person(), new Contribution { Id = "p", CampaignId = "x", MemberId = "m1", Status = "Pending" });
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Empty(await rig.Badges());

        await rig.Seed(Paid("x"));
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Equal(new[] { "FirstContribution" }, await rig.Badges());
    }

    [Fact]
    public async Task Badges_are_awarded_once_however_often_evaluation_runs()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Paid("x"));

        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        rig.Fresh();
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        rig.Fresh();
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");

        Assert.Single(await rig.Badges());
    }

    [Fact]
    public async Task Another_members_contribution_does_not_count()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Person("m2"), Paid("x", "m2"));
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Empty(await rig.Badges());
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    public async Task Three_confirmed_rsvps_earn_the_event_attendee_badge(int rsvps, bool earned)
    {
        var rig = new Rig();
        await rig.Seed(Person());
        await rig.Seed(Enumerable.Range(0, rsvps).Select(i => (object)new EventRsvp { Id = $"r{i}", EventId = $"e{i}", MemberId = "m1", Status = "Confirmed" }).ToArray());
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Equal(earned, (await rig.Badges()).Contains("EventAttendee3"));
    }

    [Fact]
    public async Task Cancelled_rsvps_do_not_count_towards_attendance()
    {
        var rig = new Rig();
        await rig.Seed(Person(),
            new EventRsvp { Id = "a", EventId = "e1", MemberId = "m1", Status = "Confirmed" },
            new EventRsvp { Id = "b", EventId = "e2", MemberId = "m1", Status = "Confirmed" },
            new EventRsvp { Id = "c", EventId = "e3", MemberId = "m1", Status = "Cancelled" });
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.DoesNotContain("EventAttendee3", await rig.Badges());
    }

    [Fact]
    public async Task One_registered_referral_earns_referrer_and_five_earn_super_referrer()
    {
        var rig = new Rig();
        await rig.Seed(Person(), new Referral { Id = "r1", ReferrerId = "m1", Status = "Registered", ReferredEmail = "a@x.com" });
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Equal(new[] { "Referrer" }, await rig.Badges());

        await rig.Seed(Enumerable.Range(2, 4).Select(i => (object)new Referral { Id = $"r{i}", ReferrerId = "m1", Status = "Registered", ReferredEmail = $"{i}@x.com" }).ToArray());
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Equal(new[] { "Referrer", "SuperReferrer" }, await rig.Badges());
    }

    [Fact]
    public async Task Referrals_that_have_gone_on_to_pay_dues_still_count_as_successful_referrals()
    {
        // A referral moves Registered -> MembershipPaid; it must not stop counting towards the badges.
        var rig = new Rig();
        await rig.Seed(new object[] { Person() }.Concat(Enumerable.Range(1, 5).Select(i => (object)new Referral { Id = $"r{i}", ReferrerId = "m1", Status = "MembershipPaid", ReferredEmail = $"{i}@x.com" })).ToArray());

        await rig.Service.EvaluateAndAwardBadgesAsync("m1");

        Assert.Equal(new[] { "Referrer", "SuperReferrer" }, await rig.Badges());
    }

    [Fact]
    public async Task Pending_referrals_that_never_registered_do_not_count()
    {
        var rig = new Rig();
        await rig.Seed(Person(), new Referral { Id = "r1", ReferrerId = "m1", Status = "Pending", ReferredEmail = "a@x.com" });
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Empty(await rig.Badges());
    }

    [Theory]
    [InlineData(1, new string[0])]
    [InlineData(2, new[] { "MembershipStreak2" })]
    [InlineData(3, new[] { "MembershipStreak2", "MembershipStreak3" })]
    [InlineData(5, new[] { "MembershipStreak2", "MembershipStreak3", "MembershipStreak5" })]
    public async Task Consecutive_years_of_dues_ending_this_year_earn_the_streak_badges(int years, string[] expectedStreakBadges)
    {
        var rig = new Rig();
        var entities = new List<object> { Person() };
        for (var i = 0; i < years; i++)
        {
            var y = ThisYear - i;
            entities.Add(Dues(y));
            entities.Add(Paid($"dues-{y}"));
        }
        await rig.Seed(entities.ToArray());

        await rig.Service.EvaluateAndAwardBadgesAsync("m1");

        var streaks = (await rig.Badges()).Where(b => b.StartsWith("MembershipStreak")).ToList();
        Assert.Equal(expectedStreakBadges.OrderBy(x => x), streaks);
    }

    [Fact]
    public async Task A_streak_that_ended_last_year_still_counts()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Dues(ThisYear - 1), Dues(ThisYear - 2), Paid($"dues-{ThisYear - 1}"), Paid($"dues-{ThisYear - 2}"));
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.Contains("MembershipStreak2", await rig.Badges());
    }

    [Fact]
    public async Task A_streak_that_ended_two_or_more_years_ago_does_not_count()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Dues(ThisYear - 3), Dues(ThisYear - 4), Paid($"dues-{ThisYear - 3}"), Paid($"dues-{ThisYear - 4}"));
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.DoesNotContain("MembershipStreak2", await rig.Badges());
    }

    [Fact]
    public async Task A_gap_in_the_years_breaks_the_streak_so_only_the_latest_run_counts()
    {
        var rig = new Rig();
        // paid: this year, last year, then a gap, then three older years -> latest run is 2 long
        var years = new[] { ThisYear, ThisYear - 1, ThisYear - 3, ThisYear - 4, ThisYear - 5 };
        var entities = new List<object> { Person() };
        foreach (var y in years) { entities.Add(Dues(y)); entities.Add(Paid($"dues-{y}")); }
        await rig.Seed(entities.ToArray());

        await rig.Service.EvaluateAndAwardBadgesAsync("m1");

        var streaks = (await rig.Badges()).Where(b => b.StartsWith("MembershipStreak")).ToList();
        Assert.Equal(new[] { "MembershipStreak2" }, streaks);
    }

    [Fact]
    public async Task Unpaid_years_in_the_middle_are_not_counted_as_paid()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Dues(ThisYear), Dues(ThisYear - 1), Dues(ThisYear - 2), Paid($"dues-{ThisYear}"), Paid($"dues-{ThisYear - 2}"));
        await rig.Service.EvaluateAndAwardBadgesAsync("m1");
        Assert.DoesNotContain("MembershipStreak2", await rig.Badges());
    }

    // ── Reading badges ──────────────────────────────────────────────────

    [Fact]
    public async Task My_badges_are_newest_first_with_the_current_member_name()
    {
        var rig = new Rig();
        await rig.Seed(Person(),
            new MemberBadge { Id = "old", MemberId = "m1", BadgeType = "FirstContribution", EarnedAt = DateTime.UtcNow.AddDays(-9), Member = new MemberSnapshot { FirstName = "Old", LastName = "Name" } },
            new MemberBadge { Id = "new", MemberId = "m1", BadgeType = "Referrer", EarnedAt = DateTime.UtcNow, Member = new MemberSnapshot { FirstName = "Old", LastName = "Name" } },
            new MemberBadge { Id = "other", MemberId = "m2", BadgeType = "Referrer", EarnedAt = DateTime.UtcNow });

        var badges = (await rig.Service.GetMyBadgesAsync("m1")).Data!;

        Assert.Equal(new[] { "Referrer", "FirstContribution" }, badges.Select(b => b.BadgeType));
        Assert.All(badges, b => Assert.Equal("Ama Mensah", b.MemberName));
    }

    [Fact]
    public async Task A_member_with_no_badges_gets_an_empty_list()
    {
        var rig = new Rig();
        var response = await rig.Service.GetMyBadgesAsync("nobody");
        Assert.Equal(200, response.Code);
        Assert.Empty(response.Data!);
    }
}
