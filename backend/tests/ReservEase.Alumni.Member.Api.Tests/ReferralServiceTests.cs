using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class ReferralServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CapturingLogger<ReferralService> Log { get; } = new();
        public Mock<ITemporalClientProvider> Temporal { get; } = new();
        public MailtrapConfig Mail { get; } = new();
        public AuthData Me { get; } = new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };
        public ReferralService Service { get; private set; } = null!;

        public Rig(Action<Institution>? institution = null)
        {
            Temporal.SetupGet(t => t.IsAvailable).Returns(false);   // enqueue is logged as dropped, which tests can see
            var inst = new Institution { Id = Tenant, Slug = "umat", Name = "UMaT", PortalName = "UMaT Portal", PrimaryColorHex = "#112233" };
            institution?.Invoke(inst);
            using (var seed = TestDb.Create(DbName, Tenant)) { seed.Institutions.Add(inst); seed.SaveChanges(); }
            Fresh();
        }

        public ReferralService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            var http = new DefaultHttpContext();
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("umat.members.test");
            return Service = new ReferralService(
                new AlumniPgRepository<Referral>(db), new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<MemberBadge>(db),
                new AlumniPgRepository<Institution>(db), TestDb.Tenant(Tenant, "umat"),
                Mock.Of<IHttpContextAccessor>(a => a.HttpContext == http), Options.Create(Mail), Temporal.Object, Log);
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

    private static MemberEntity Person(string id = "m1", string first = "Ama", string last = "Mensah", string? code = null) =>
        new() { Id = id, FirstName = first, LastName = last, Email = $"{id}@x.com", ReferralCode = code };

    private static Referral Ref(string id, string referrer, string status, string? referred = null) =>
        new() { Id = id, ReferrerId = referrer, Status = status, ReferredEmail = $"{id}@x.com", ReferredMemberId = referred };

    // ── Points ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Points_are_registration_points_plus_a_bonus_for_those_who_paid_dues_and_nothing_for_pending()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Ref("a", "m1", "Pending"), Ref("b", "m1", "Registered"), Ref("c", "m1", "MembershipPaid"));

        var info = (await rig.Service.GetMyReferralInfoAsync(rig.Me)).Data!;

        var expected = ReferralPoints.Registered + ReferralPoints.Registered + ReferralPoints.MembershipBonus;
        Assert.Equal(expected, info.Points);
        Assert.Equal((3, 1, 2, 1), (info.TotalReferrals, info.PendingReferrals, info.RegisteredReferrals, info.MembershipPaidReferrals));
    }

    [Fact]
    public async Task A_member_with_no_referrals_has_zero_points_and_no_rank()
    {
        var rig = new Rig();
        await rig.Seed(Person());
        var info = (await rig.Service.GetMyReferralInfoAsync(rig.Me)).Data!;
        Assert.Equal((0, null, false), (info.Points, info.Rank, info.HasReferrerBadge));
    }

    [Fact]
    public async Task Rank_is_one_plus_the_number_of_referrers_with_strictly_more_points_so_ties_share_a_rank()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Person("m2"), Person("m3"), Person("m4"),
            Ref("a", "m1", "Registered"),
            Ref("b", "m2", "MembershipPaid"),                        // more than m1
            Ref("c", "m3", "Registered"),                            // ties m1
            Ref("d", "m4", "Registered"), Ref("e", "m4", "Registered"));   // more than m1

        var info = (await rig.Service.GetMyReferralInfoAsync(rig.Me)).Data!;

        Assert.Equal(3, info.Rank);
    }

    [Fact]
    public async Task The_referrer_badge_flag_reflects_whether_the_badge_was_awarded()
    {
        var rig = new Rig();
        await rig.Seed(Person(), new MemberBadge { Id = "b", MemberId = "m1", BadgeType = "Referrer" });
        Assert.True((await rig.Service.GetMyReferralInfoAsync(rig.Me)).Data!.HasReferrerBadge);
    }

    [Fact]
    public async Task A_first_look_generates_a_stable_referral_code_from_the_name()
    {
        var rig = new Rig();
        await rig.Seed(Person(first: "Ama", last: "Mensah"));

        var first = (await rig.Service.GetMyReferralInfoAsync(rig.Me)).Data!.ReferralCode;
        rig.Fresh();
        var second = (await rig.Service.GetMyReferralInfoAsync(rig.Me)).Data!.ReferralCode;

        Assert.Matches("^AMAMEN-[0-9A-F]{6}$", first);
        Assert.Equal(first, second);
        Assert.Equal(first, (await rig.Db().Members.SingleAsync()).ReferralCode);
    }

    [Fact]
    public async Task Short_names_do_not_break_code_generation_and_an_existing_code_is_kept()
    {
        var rig = new Rig();
        await rig.Seed(Person(first: "Al", last: "O"), Person("m2", code: "KEEP-1"));

        Assert.Matches("^ALO-[0-9A-F]{6}$", (await rig.Service.GetMyReferralInfoAsync(new AuthData { Id = "m1", FirstName = "Al", LastName = "O" })).Data!.ReferralCode);
        Assert.Equal("KEEP-1", (await rig.Service.GetMyReferralInfoAsync(new AuthData { Id = "m2", FirstName = "X", LastName = "Y" })).Data!.ReferralCode);
    }

    [Fact]
    public async Task Referral_info_for_an_unknown_member_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.GetMyReferralInfoAsync(rig.Me)).Code);
    }

    // ── Leaderboard ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_leaderboard_ranks_by_points_then_by_members_who_paid_dues_and_skips_zero_point_referrers()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1", "Ama", "A"), Person("m2", "Kofi", "B"), Person("m3", "Esi", "C"), Person("m4", "Yaw", "D"),
            Ref("a", "m1", "Registered"), Ref("b", "m1", "Registered"), Ref("c", "m1", "Registered"),                        // 3 x Registered
            Ref("d", "m2", "MembershipPaid"), Ref("e", "m2", "Registered"),                                                  // same points, more paid
            Ref("f", "m3", "Pending"));                                                                                       // 0 points

        var board = (await rig.Service.GetLeaderboardAsync()).Data!;

        // m2: MembershipPaid (25) + Registered (10) = 35; m1: three Registered = 30; m3 has only a pending referral
        Assert.Equal(new[] { "m2", "m1" }, board.Select(e => e.MemberId));
        Assert.Equal(new[] { 35, 30 }, board.Select(e => e.Points));
        Assert.Equal(Enumerable.Range(1, board.Count), board.Select(e => e.Rank));
    }

    [Fact]
    public async Task Leaderboard_entries_show_names_and_counts_with_a_placeholder_for_deleted_members()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1", "Ama", "Mensah"), Ref("a", "m1", "MembershipPaid"), Ref("b", "gone", "Registered"));

        var board = (await rig.Service.GetLeaderboardAsync()).Data!.ToDictionary(e => e.MemberId);

        Assert.Equal(("Ama Mensah", 1, 1), (board["m1"].Name, board["m1"].TotalReferrals, board["m1"].MembershipPaidReferrals));
        Assert.Equal("Former member", board["gone"].Name);
    }

    [Fact]
    public async Task The_leaderboard_is_capped_at_twenty_entries()
    {
        var rig = new Rig();
        var seed = new List<object>();
        for (var i = 0; i < 25; i++) { seed.Add(Person($"m{i}")); seed.Add(Ref($"r{i}", $"m{i}", "Registered")); }
        await rig.Seed(seed.ToArray());

        Assert.Equal(20, (await rig.Service.GetLeaderboardAsync()).Data!.Count);
    }

    [Fact]
    public async Task An_empty_leaderboard_is_an_empty_list()
        => Assert.Empty((await new Rig().Service.GetLeaderboardAsync()).Data!);

    // ── Invitations ─────────────────────────────────────────────────────

    [Fact]
    public async Task Inviting_a_new_email_records_a_pending_referral_and_queues_the_invitation_email()
    {
        var rig = new Rig();
        await rig.Seed(Person());

        var response = await rig.Service.InviteAsync("  New.Person@Example.COM ", rig.Me);

        Assert.Equal(201, response.Code);
        var referral = await rig.Db().Referrals.SingleAsync();
        Assert.Equal(("new.person@example.com", "Pending", "m1", "m1"), (referral.ReferredEmail, referral.Status, referral.ReferrerId, referral.CreatedBy));
        Assert.Equal("Ama Mensah", $"{referral.Referrer!.FirstName} {referral.Referrer.LastName}");
        Assert.Contains(rig.Log.Entries, e => e.Message.Contains("Email") && e.Message.Contains("dropped"));   // handed to the notification pipeline
    }

    [Fact]
    public async Task Inviting_creates_the_referrers_code_when_they_do_not_have_one_yet()
    {
        var rig = new Rig();
        await rig.Seed(Person());
        await rig.Service.InviteAsync("a@b.com", rig.Me);
        Assert.Matches("^AMAMEN-", (await rig.Db().Members.SingleAsync()).ReferralCode);
    }

    [Fact]
    public async Task Inviting_someone_who_is_already_a_member_is_refused()
    {
        var rig = new Rig();
        var existing = Person("m2"); existing.Email = "taken@x.com";
        await rig.Seed(Person(), existing);

        var response = await rig.Service.InviteAsync("TAKEN@x.com", rig.Me);

        Assert.Equal(400, response.Code);
        Assert.Contains("already a registered member", response.Message);
        Assert.Equal(0, await rig.Db().Referrals.CountAsync());
    }

    [Fact]
    public async Task Inviting_the_same_email_twice_is_refused_but_a_different_referrer_may_invite_them()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Person("m2"));
        await rig.Service.InviteAsync("friend@x.com", rig.Me);
        rig.Fresh();

        var again = await rig.Service.InviteAsync("friend@x.com", rig.Me);
        Assert.Equal(400, again.Code);
        Assert.Contains("already sent an invitation", again.Message);

        rig.Fresh();
        var other = await rig.Service.InviteAsync("friend@x.com", new AuthData { Id = "m2", FirstName = "Kofi", LastName = "B", Email = "m2@x.com" });
        Assert.Equal(201, other.Code);
    }

    [Fact]
    public async Task An_unknown_referrer_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.InviteAsync("a@b.com", rig.Me)).Code);
    }

    [Fact]
    public async Task With_institution_email_switched_off_the_invite_is_recorded_but_no_email_is_queued()
    {
        var rig = new Rig(i => i.EmailNotificationsEnabled = false);
        await rig.Seed(Person());

        var response = await rig.Service.InviteAsync("a@b.com", rig.Me);

        Assert.Equal(201, response.Code);
        Assert.Equal(1, await rig.Db().Referrals.CountAsync());
        Assert.DoesNotContain(rig.Log.Entries, e => e.Message.Contains("dropped"));
        Assert.Contains(rig.Log.Entries, e => e.Message.Contains("skipped"));
    }

    // ── My referrals ────────────────────────────────────────────────────

    [Fact]
    public async Task My_referrals_are_newest_first_with_live_names_for_me_and_the_people_I_referred()
    {
        var rig = new Rig();
        await rig.Seed(Person(), Person("m2", "Kofi", "Boateng"),
            new Referral { Id = "old", ReferrerId = "m1", ReferredEmail = "o@x.com", Status = "Registered", ReferredMemberId = "m2", CreatedAt = DateTime.UtcNow.AddDays(-3), Referrer = new MemberSnapshot { FirstName = "Stale" } },
            new Referral { Id = "new", ReferrerId = "m1", ReferredEmail = "n@x.com", Status = "Pending", CreatedAt = DateTime.UtcNow },
            Ref("other", "someone-else", "Registered"));

        var list = (await rig.Service.GetMyReferralsAsync("m1")).Data!;

        Assert.Equal(new[] { "new", "old" }, list.Select(r => r.Id));
        Assert.All(list, r => Assert.Equal("Ama Mensah", r.ReferrerName));
        Assert.Equal("Kofi Boateng", list.Single(r => r.Id == "old").ReferredMemberName);
    }

    [Fact]
    public async Task A_member_with_no_referrals_gets_an_empty_list()
        => Assert.Empty((await new Rig().Service.GetMyReferralsAsync("nobody")).Data!);
}
