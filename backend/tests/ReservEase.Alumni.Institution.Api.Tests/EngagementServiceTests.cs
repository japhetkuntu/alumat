using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.Institution.Api.Engagement;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class EngagementServiceTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly AuthData Super = new() { Id = "super", FirstName = "Ama", LastName = "Admin", Role = "SuperAdmin" };
    private static int next;

    private static (EngagementService service, string db) Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            foreach (var e in seed) db.Add(e);
            db.SaveChanges();
        }
        return (Build(name, Tenant), name);
    }

    private static EngagementService Build(string db, string tenant, InMemoryRedisService<InstitutionRedisConfig>? cache = null)
    {
        var ctx = TestDb.Create(db, tenant);
        var tenantService = TestDb.Tenant(tenant);
        var engine = new EngagementEngine(
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<ForumThread>(ctx), new AlumniPgRepository<ForumPost>(ctx),
            new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<ClassNote>(ctx),
            new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<CampaignUpdate>(ctx),
            new AlumniPgRepository<NewsPost>(ctx), new AlumniPgRepository<Job>(ctx), new AlumniPgRepository<CommunityHealthSnapshot>(ctx),
            new AlumniPgRepository<EngagementRecommendation>(ctx), new AlumniPgRepository<StaffEntity>(ctx), new AlumniPgRepository<StaffActivityWeek>(ctx),
            new AlumniPgRepository<Referral>(ctx), tenantService);
        return new EngagementService(
            engine, new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<CommunityHealthSnapshot>(ctx),
            new AlumniPgRepository<EngagementRecommendation>(ctx), new AlumniPgRepository<EngagementChecklistEntry>(ctx),
            new AlumniPgRepository<StaffEntity>(ctx), tenantService, cache ?? new InMemoryRedisService<InstitutionRedisConfig>(),
            NullLogger<EngagementService>.Instance);
    }

    private static MemberEntity Person(string tenant = Tenant, string status = "Active", DateTime? joined = null, DateTime? lastLogin = null, string? bio = null, string? id = null) => new()
    {
        Id = id ?? $"m{Interlocked.Increment(ref next)}", InstitutionId = tenant, Status = status, FirstName = "A", LastName = "B",
        Email = $"{Guid.NewGuid():N}@x.com", CreatedAt = joined ?? Now.AddYears(-1), LastLoginAt = lastLogin, Bio = bio, IsEmailVerified = true,
    };

    private static async Task<EngagementDashboardDto> Dashboard(EngagementService s, int days = 30) => (await s.GetDashboardAsync(Super, days, [])).Data!;

    [Fact]
    public async Task A_community_with_activity_is_read_from_the_records_it_already_has()
    {
        var people = Enumerable.Range(0, 10).Select(_ => Person(lastLogin: Now.AddDays(-3), bio: "hi")).ToList();
        var (s, _) = Create(people.Cast<object>().Concat([
            new ForumThread { InstitutionId = Tenant, AuthorId = people[0].Id, CreatedAt = Now.AddDays(-2) },
            new ForumPost { InstitutionId = Tenant, AuthorId = people[1].Id, CreatedAt = Now.AddDays(-3) },
            new EventRsvp { InstitutionId = Tenant, MemberId = people[2].Id, Status = "Confirmed", CreatedAt = Now.AddDays(-4) },
            new EventRsvp { InstitutionId = Tenant, MemberId = people[3].Id, Status = "Cancelled", CreatedAt = Now.AddDays(-4) },
            new Contribution { InstitutionId = Tenant, MemberId = people[4].Id, Status = "Successful", Amount = 50, ConfirmedAt = Now.AddDays(-1) },
            new Contribution { InstitutionId = Tenant, MemberId = people[5].Id, Status = "Pending", Amount = 70, CreatedAt = Now.AddDays(-1) },
        ]).ToArray());

        var d = await Dashboard(s);

        Assert.Equal(10, d.ActiveMembers);
        Assert.Equal(10, d.Activated);
        Assert.Equal(4, d.Participants.Current);          // thread, reply, confirmed RSVP, successful gift; not the cancelled RSVP or pending payment
        Assert.Equal(50m, d.ContributionVolume);
        Assert.Equal(1, d.Contributors);
        Assert.NotNull(d.Health.Score);
    }

    [Fact]
    public async Task Another_institutions_activity_never_counts()
    {
        var mine = Person(lastLogin: Now.AddDays(-1));
        var theirs = Person(Other, lastLogin: Now.AddDays(-1));
        var (s, _) = Create(mine, theirs,
            new ForumThread { InstitutionId = Other, AuthorId = theirs.Id, CreatedAt = Now.AddDays(-1) },
            new Contribution { InstitutionId = Other, MemberId = theirs.Id, Status = "Successful", Amount = 999, ConfirmedAt = Now.AddDays(-1) },
            new EngagementRecommendation { InstitutionId = Other, DedupeKey = "x", Title = "Not yours" });

        var d = await Dashboard(s);

        Assert.Equal(1, d.ActiveMembers);
        Assert.Equal(0, d.Participants.Current);
        Assert.Equal(0m, d.ContributionVolume);
        Assert.DoesNotContain(d.Recommendations, r => r.Title == "Not yours");
    }

    [Fact]
    public async Task A_new_community_shows_insufficient_data_rather_than_a_low_score()
    {
        var (s, _) = Create(Person(), Person());

        var d = await Dashboard(s);

        Assert.Null(d.Health.Score);
        Assert.Equal(HealthClassifications.InsufficientData, d.Health.Classification);
    }

    [Fact]
    public async Task Opening_the_dashboard_twice_the_same_day_keeps_one_reading_and_one_of_each_recommendation()
    {
        var people = Enumerable.Range(0, 8).Select(_ => Person(joined: Now.AddDays(-2))).Cast<object>().ToArray();
        var (s, db) = Create(people);

        await Dashboard(s);
        var second = Build(db, Tenant); // a fresh service has an empty cache, as after five minutes
        var d = await Dashboard(second);

        using var check = TestDb.Create(db, Tenant);
        Assert.Equal(1, check.Set<CommunityHealthSnapshot>().IgnoreQueryFilters().Count());
        Assert.Single(d.Recommendations, r => r.RuleId == "welcome-new-members");
        Assert.Equal(d.Recommendations.Select(r => r.Id).Distinct().Count(), d.Recommendations.Count);
    }

    [Fact]
    public async Task A_completed_recommendation_is_recorded_with_who_did_it_and_does_not_come_straight_back()
    {
        var people = Enumerable.Range(0, 8).Select(_ => Person(joined: Now.AddDays(-2))).Cast<object>().ToArray();
        var (s, db) = Create(people);
        var rec = (await Dashboard(s)).Recommendations.Single(r => r.RuleId == "welcome-new-members");

        var done = (await s.ResolveRecommendationAsync(Super, rec.Id, "complete", null)).Data!;
        var after = await Dashboard(Build(db, Tenant));

        Assert.Equal(RecommendationStatuses.Completed, done.Status);
        Assert.Equal("Ama Admin", done.ResolvedByName);
        Assert.DoesNotContain(after.Recommendations, r => r.RuleId == "welcome-new-members");
        Assert.Contains((await s.ListRecommendationsAsync(RecommendationStatuses.Completed, 1, 20)).Data!.Results, r => r.Id == rec.Id);
    }

    [Fact]
    public async Task Snoozing_hides_a_recommendation_and_an_unknown_action_is_refused()
    {
        var people = Enumerable.Range(0, 8).Select(_ => Person(joined: Now.AddDays(-2))).Cast<object>().ToArray();
        var (s, db) = Create(people);
        var rec = (await Dashboard(s)).Recommendations.First();

        var snoozed = (await s.ResolveRecommendationAsync(Super, rec.Id, "snooze", 5)).Data!;
        var refused = await s.ResolveRecommendationAsync(Super, rec.Id, "explode", null);

        Assert.Equal(RecommendationStatuses.Snoozed, snoozed.Status);
        Assert.InRange((snoozed.SnoozedUntil!.Value - Now).TotalDays, 4.9, 5.1);
        Assert.DoesNotContain((await Dashboard(Build(db, Tenant))).Recommendations, r => r.Id == rec.Id);
        Assert.Equal(400, refused.Code);
    }

    [Fact]
    public async Task One_institution_cannot_resolve_or_assign_anothers_recommendation()
    {
        var foreign = new EngagementRecommendation { Id = "foreign", InstitutionId = Other, DedupeKey = "z", Title = "Theirs" };
        var theirStaff = new StaffEntity { Id = "other-staff", InstitutionId = Other, FirstName = "X", LastName = "Y" };
        var mine = new EngagementRecommendation { Id = "mine", InstitutionId = Tenant, DedupeKey = "a", Title = "Mine" };
        var (s, _) = Create(foreign, theirStaff, mine);

        Assert.Equal(404, (await s.ResolveRecommendationAsync(Super, "foreign", "dismiss", null)).Code);
        Assert.Equal(400, (await s.AssignRecommendationAsync(Super, "mine", "other-staff")).Code);
    }

    [Fact]
    public async Task A_recommendation_can_be_delegated_to_a_colleague_and_taken_back()
    {
        var colleague = new StaffEntity { Id = "s2", InstitutionId = Tenant, FirstName = "Kojo", LastName = "Mensah" };
        var (s, _) = Create(colleague, new EngagementRecommendation { Id = "r1", InstitutionId = Tenant, DedupeKey = "a", Title = "T" });

        var assigned = (await s.AssignRecommendationAsync(Super, "r1", "s2")).Data!;
        var cleared = (await s.AssignRecommendationAsync(Super, "r1", null)).Data!;

        Assert.Equal("Kojo Mensah", assigned.AssignedToName);
        Assert.Null(cleared.AssignedToId);
    }

    [Fact]
    public async Task Ticking_a_checklist_item_sticks_for_the_week_and_can_be_undone()
    {
        var people = Enumerable.Range(0, 8).Select(_ => Person(joined: Now.AddDays(-1))).Cast<object>().ToArray();
        var (s, db) = Create(people);

        await s.SetChecklistItemAsync(Super, "welcome-new-members", true);
        await s.SetChecklistItemAsync(Super, "welcome-new-members", true); // twice is harmless
        Assert.True((await Dashboard(Build(db, Tenant))).Checklist.Single(i => i.Key == "welcome-new-members").Done);

        await s.SetChecklistItemAsync(Super, "welcome-new-members", false);
        Assert.False((await Dashboard(Build(db, Tenant))).Checklist.Single(i => i.Key == "welcome-new-members").Done);
    }

    [Fact]
    public async Task The_health_history_lists_readings_oldest_first_and_only_for_this_institution()
    {
        var day = new DateTime(Now.Year, Now.Month, Now.Day, 0, 0, 0, DateTimeKind.Utc);
        var (s, _) = Create(
            new CommunityHealthSnapshot { InstitutionId = Tenant, PeriodDays = 30, SnapshotDate = day.AddDays(-2), Score = 40, Classification = HealthClassifications.AtRisk },
            new CommunityHealthSnapshot { InstitutionId = Tenant, PeriodDays = 30, SnapshotDate = day.AddDays(-1), Score = 55, Classification = HealthClassifications.NeedsAttention },
            new CommunityHealthSnapshot { InstitutionId = Other, PeriodDays = 30, SnapshotDate = day.AddDays(-1), Score = 99, Classification = HealthClassifications.Healthy });

        var history = (await s.GetHealthHistoryAsync(30, 60)).Data!;

        Assert.Equal([40, 55], history.Select(h => h.Score));
    }

    [Fact]
    public async Task An_unsupported_period_falls_back_to_thirty_days()
    {
        var (s, _) = Create(Person());

        Assert.Equal(30, (await Dashboard(s, 12)).PeriodDays);
    }

    // ── Ambassadors and year groups ─────────────────────────────────────────

    private static MemberEntity Classmate(int year, string tenant = Tenant, string status = "Active", DateTime? joined = null) =>
        new() { Id = $"c{Interlocked.Increment(ref next)}", InstitutionId = tenant, Status = status, GraduationYear = year, FirstName = "Kofi", LastName = $"Y{year}",
                Email = $"{Guid.NewGuid():N}@x.com", CreatedAt = joined ?? Now.AddYears(-1), LastLoginAt = Now.AddDays(-2), IsEmailVerified = true };

    private static StaffEntity Ambassador(string id, int[] years, string tenant = Tenant, DateTime? lastLogin = null, bool disabled = false) =>
        new() { Id = id, InstitutionId = tenant, FirstName = "Esi", LastName = id, Role = "ScopedAdmin", YearGroups = years.ToList(), LastLoginAt = lastLogin ?? Now.AddDays(-1), IsDisabled = disabled };

    private static IEnumerable<object> Year(int year, int count, string tenant = Tenant) => Enumerable.Range(0, count).Select(_ => (object)Classmate(year, tenant));

    [Fact]
    public async Task Year_groups_show_who_covers_them_and_which_have_nobody()
    {
        var (s, _) = Create(Year(2015, 6).Concat(Year(2016, 4)).Concat(Year(2017, 1)).Append(Ambassador("a1", [2015])).ToArray());

        var c = (await s.GetCohortsAsync(Super, 30, [])).Data!;

        Assert.Equal(2, c.CohortsWithMembers);               // 2017 has one member: too few to need an ambassador
        Assert.Equal(1, c.CohortsCovered);
        Assert.Single(c.Cohorts.Single(x => x.Year == 2015).Ambassadors);
        Assert.Empty(c.Cohorts.Single(x => x.Year == 2016).Ambassadors);
    }

    [Fact]
    public async Task Ambassador_coverage_enters_the_health_score_once_year_groups_exist()
    {
        var (s, _) = Create(Year(2015, 6).Concat(Year(2016, 4)).Append(Ambassador("a1", [2015])).ToArray());

        var d = await Dashboard(s);

        var factor = d.Health.Factors.Single(f => f.Key == "ambassadors");
        Assert.NotNull(factor.Score);
        Assert.Contains("1 of 2 year groups", factor.Detail);
    }

    [Fact]
    public async Task An_institution_without_year_groups_leaves_ambassadors_out_of_the_score()
    {
        var (s, _) = Create(Year(2015, 6).Concat([Ambassador("a1", [2015])]).ToArray());

        var d = (await s.GetDashboardAsync(Super, 30, [], usesYearGroups: false)).Data!;

        Assert.Null(d.Health.Factors.Single(f => f.Key == "ambassadors").Score);
        Assert.DoesNotContain(d.Recommendations, r => r.RuleId is "unassigned-cohort" or "inactive-ambassador");
    }

    [Fact]
    public async Task Uncovered_year_groups_and_a_quiet_ambassador_each_raise_a_suggestion()
    {
        var (s, _) = Create(Year(2015, 6).Concat(Year(2016, 5)).Append(Ambassador("a1", [2015], lastLogin: Now.AddDays(-60))).ToArray());

        var d = await Dashboard(s);

        Assert.Contains(d.Recommendations, r => r.RuleId == "unassigned-cohort" && r.Title.StartsWith("1 year group has no ambassador"));
        Assert.Contains(d.Recommendations, r => r.RuleId == "inactive-ambassador" && r.Title.Contains("60 days"));
    }

    [Fact]
    public async Task A_disabled_ambassador_no_longer_counts_as_cover()
    {
        var (s, _) = Create(Year(2015, 6).Append(Ambassador("a1", [2015], disabled: true)).ToArray());

        var c = (await s.GetCohortsAsync(Super, 30, [])).Data!;

        Assert.Equal(0, c.CohortsCovered);
        Assert.Empty(c.Ambassadors);
    }

    [Fact]
    public async Task An_ambassador_sees_only_their_own_year_groups_and_nothing_of_other_institutions()
    {
        var me = new AuthData { Id = "a1", FirstName = "Esi", LastName = "a1", Role = "ScopedAdmin", YearGroups = [2015] };
        var (s, _) = Create(Year(2015, 6).Concat(Year(2016, 7)).Concat(Year(2015, 9, Other))
            .Append(Ambassador("a1", [2015])).Append(Ambassador("a2", [2016])).ToArray());

        var w = (await s.GetAmbassadorWorkspaceAsync(me, 30, [])).Data!;

        Assert.Equal([2015], w.Cohorts.Cohorts.Select(c => c.Year));
        Assert.Equal(6, w.Cohorts.Cohorts.Single().ActiveMembers);   // not the 9 at the other institution
        Assert.Equal(["a1"], w.Cohorts.Ambassadors.Select(a => a.StaffId));
        Assert.All(w.RecentlyJoined, m => Assert.Equal(2015, m.Year));
    }

    [Fact]
    public async Task An_ambassador_can_finish_only_a_task_delegated_to_them()
    {
        var me = new AuthData { Id = "a1", FirstName = "Esi", LastName = "a1", Role = "ScopedAdmin", YearGroups = [2015] };
        var (s, _) = Create(
            new EngagementRecommendation { Id = "mine", InstitutionId = Tenant, DedupeKey = "a", Title = "Mine", AssignedToId = "a1" },
            new EngagementRecommendation { Id = "theirs", InstitutionId = Tenant, DedupeKey = "b", Title = "Theirs", AssignedToId = "a2" },
            new EngagementRecommendation { Id = "foreign", InstitutionId = Other, DedupeKey = "c", Title = "Foreign", AssignedToId = "a1" });

        Assert.Equal(RecommendationStatuses.Completed, (await s.UpdateMyTaskAsync(me, "mine", "complete")).Data!.Status);
        Assert.Equal(404, (await s.UpdateMyTaskAsync(me, "theirs", "complete")).Code);
        Assert.Equal(404, (await s.UpdateMyTaskAsync(me, "foreign", "complete")).Code);
        Assert.Equal(400, (await s.UpdateMyTaskAsync(me, "mine", "delete")).Code);
    }

    [Fact]
    public async Task Invitations_that_became_registrations_are_credited_to_the_inviters_year_group()
    {
        var inviter = Classmate(2015);
        var (s, _) = Create(Year(2015, 5).Append(inviter).Append(new Referral { InstitutionId = Tenant, ReferrerId = inviter.Id, ReferredMemberId = "new1", ReferredEmail = "n@x.com", Status = "Registered", CreatedAt = Now.AddDays(-3) })
            .Append(new Referral { InstitutionId = Tenant, ReferrerId = inviter.Id, ReferredEmail = "p@x.com", Status = "Pending", CreatedAt = Now.AddDays(-3) }).ToArray());

        var c = (await s.GetCohortsAsync(Super, 30, [])).Data!;

        Assert.Equal(1, c.Cohorts.Single(x => x.Year == 2015).InvitationsRegistered); // the unregistered one is not counted
    }

    // ── Monthly report ──────────────────────────────────────────────────────

    private static DateTime Mid(int monthsAgo) { var d = new DateTime(Now.Year, Now.Month, 15, 12, 0, 0, DateTimeKind.Utc).AddMonths(-monthsAgo); return d; }

    [Fact]
    public async Task A_months_figures_are_compared_with_the_month_before_and_exclude_everything_outside_it()
    {
        var a = Classmate(2015); var b = Classmate(2015); var c = Classmate(2015);
        var (s, _) = Create(a, b, c,
            new ForumThread { InstitutionId = Tenant, AuthorId = a.Id, CreatedAt = Mid(1) },
            new ForumThread { InstitutionId = Tenant, AuthorId = b.Id, CreatedAt = Mid(1) },
            new ForumThread { InstitutionId = Tenant, AuthorId = a.Id, CreatedAt = Mid(2) },
            new NewsPost { InstitutionId = Tenant, Title = "n", Status = "Published", CreatedAt = Mid(1) },
            new NewsPost { InstitutionId = Tenant, Title = "draft", Status = "Draft", CreatedAt = Mid(1) });
        var month = Mid(1).ToString("yyyy-MM");

        var r = (await s.GetMonthlyReportAsync(Super, month, [])).Data!;

        Assert.Equal((month, false), (r.Month, r.IsCurrentMonth));
        Assert.Equal((2, 2), (r.Current.Participants, r.Current.MeaningfulActions));
        Assert.Equal((1, 1), (r.Previous.Participants, r.Previous.MeaningfulActions));
        Assert.Equal(1, r.Current.NewsPublished);   // the draft is not "published"
    }

    [Fact]
    public async Task Money_is_shown_as_collected_fees_deducted_and_net_and_never_as_platform_revenue()
    {
        var m = Classmate(2015);
        var (s, _) = Create(m,
            new Contribution { InstitutionId = Tenant, MemberId = m.Id, Status = "Successful", Amount = 100, PlatformFeeAmount = 5, NetAmountToInstitution = 95, PlatformRevenueAmount = 7, ConfirmedAt = Mid(0) },
            new Contribution { InstitutionId = Tenant, MemberId = m.Id, Status = "Pending", Amount = 999, ConfirmedAt = Mid(0) },
            new Contribution { InstitutionId = Tenant, MemberId = m.Id, Status = "Successful", Amount = 50, NetAmountToInstitution = 50, ConfirmedAt = Mid(2) });

        var r = (await s.GetMonthlyReportAsync(Super, null, [])).Data!;

        Assert.True(r.IsCurrentMonth);
        Assert.Equal((100m, 5m, 95m, 1), (r.Current.ContributionGross, r.Current.FeesDeducted, r.Current.NetToInstitution, r.Current.Contributors));
        Assert.DoesNotContain("Revenue", string.Join(",", typeof(MonthFigures).GetProperties().Select(p => p.Name)));
    }

    [Fact]
    public async Task Another_institutions_month_never_leaks_in()
    {
        var (s, _) = Create(Classmate(2015),
            new ForumThread { InstitutionId = Other, AuthorId = "x", CreatedAt = Mid(0) },
            new Contribution { InstitutionId = Other, MemberId = "x", Status = "Successful", Amount = 500, NetAmountToInstitution = 500, ConfirmedAt = Mid(0) },
            new NewsPost { InstitutionId = Other, Title = "theirs", Status = "Published", CreatedAt = Mid(0) });

        var r = (await s.GetMonthlyReportAsync(Super, null, [])).Data!;

        Assert.Equal((0, 0m, 0), (r.Current.MeaningfulActions, r.Current.ContributionGross, r.Current.NewsPublished));
    }

    [Theory]
    [InlineData("2026-13")] [InlineData("last month")] [InlineData("2999-01")] [InlineData("2001-01")]
    public async Task A_nonsense_far_off_or_future_month_is_refused(string month)
    {
        var (s, _) = Create(Classmate(2015));

        Assert.Equal(400, (await s.GetMonthlyReportAsync(Super, month, [])).Code);
    }

    [Fact]
    public async Task Health_at_the_start_and_end_of_the_month_come_from_the_recorded_readings_and_are_blank_when_there_are_none()
    {
        var first = new DateTime(Mid(1).Year, Mid(1).Month, 2, 0, 0, 0, DateTimeKind.Utc);
        var (s, _) = Create(Classmate(2015),
            new CommunityHealthSnapshot { InstitutionId = Tenant, PeriodDays = 30, SnapshotDate = first, Score = 40, Classification = "AtRisk" },
            new CommunityHealthSnapshot { InstitutionId = Tenant, PeriodDays = 30, SnapshotDate = first.AddDays(20), Score = 62, Classification = "NeedsAttention" });

        var withReadings = (await s.GetMonthlyReportAsync(Super, Mid(1).ToString("yyyy-MM"), [])).Data!;
        var without = (await s.GetMonthlyReportAsync(Super, Mid(3).ToString("yyyy-MM"), [])).Data!;

        Assert.Equal((40, 62, 2), (withReadings.Current.HealthStart, withReadings.Current.HealthEnd, withReadings.Current.HealthReadings));
        Assert.Equal((null, null, 0), (without.Current.HealthStart, without.Current.HealthEnd, without.Current.HealthReadings));
    }

    [Fact]
    public async Task The_report_names_the_weakest_factors_and_the_next_actions()
    {
        var people = Enumerable.Range(0, 8).Select(_ => (object)Classmate(2015, joined: Now.AddDays(-2))).ToArray();
        var (s, _) = Create(people);

        var r = (await s.GetMonthlyReportAsync(Super, null, [])).Data!;

        Assert.All(r.NeedsAttention, f => Assert.True(f.Score < 60));
        Assert.NotEmpty(r.NextActions);
    }

    // ── Concurrency: the page asks for several things at once ───────────────

    // NOTE: the in-memory database cannot interleave requests the way Postgres does, so this checks the outcome (one reading, no
    // duplicate suggestions) but would pass without the lock. The real race was reproduced and fixed against Postgres: see the live checks.
    [Fact]
    public async Task Two_requests_at_once_on_a_fresh_day_both_succeed_and_leave_one_reading_and_no_duplicate_suggestions()
    {
        var name = TestDb.NewName();
        using (var seed = TestDb.Create(name, Tenant))
        {
            foreach (var m in Enumerable.Range(0, 8)) seed.Add(Classmate(2015, joined: Now.AddDays(-2)));
            seed.SaveChanges();
        }
        var sharedCache = new InMemoryRedisService<InstitutionRedisConfig>();
        EngagementService Fresh() { var s = Build(name, Tenant, sharedCache); return s; }

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => i % 2 == 0
            ? Fresh().GetDashboardAsync(Super, 30, []).ContinueWith(t => (int)t.Result.Code)
            : Fresh().GetCohortsAsync(Super, 30, []).ContinueWith(t => (int)t.Result.Code)));

        Assert.All(results, code => Assert.Equal(200, code));
        using var check = TestDb.Create(name, Tenant);
        Assert.Equal(1, check.Set<CommunityHealthSnapshot>().IgnoreQueryFilters().Count());
        var live = check.Set<EngagementRecommendation>().IgnoreQueryFilters().Where(r => r.Status == RecommendationStatuses.Open).ToList();
        Assert.Equal(live.Count, live.Select(r => r.DedupeKey).Distinct().Count());
    }

    private static PostgresException UniqueViolation() => new("duplicate key", "ERROR", "ERROR", "23505");

    [Fact]
    public async Task Losing_the_race_to_record_todays_reading_updates_the_winners_row_instead_of_failing()
    {
        var today = DateTime.UtcNow.Date;
        var winner = new CommunityHealthSnapshot { InstitutionId = Tenant, PeriodDays = 30, SnapshotDate = today, Score = 1, Classification = "x" };
        var snapshots = new Mock<IAlumniPgRepository<CommunityHealthSnapshot>>();
        var calls = 0;
        snapshots.Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CommunityHealthSnapshot, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(() => ++calls == 1 ? null : winner);
        snapshots.Setup(r => r.AddAsync(It.IsAny<CommunityHealthSnapshot>())).ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("conflict", UniqueViolation()));
        var realSnapshots = new AlumniPgRepository<CommunityHealthSnapshot>(TestDb.Create(TestDb.NewName(), Tenant));
        snapshots.Setup(r => r.GetQueryable(It.IsAny<System.Linq.Expressions.Expression<Func<CommunityHealthSnapshot, bool>>>(), It.IsAny<bool>())).Returns<System.Linq.Expressions.Expression<Func<CommunityHealthSnapshot, bool>>, bool>((p, i) => realSnapshots.GetQueryable(p, i));
        var engine = EngineWith(snapshots.Object, Mock.Of<IAlumniPgRepository<EngagementRecommendation>>());

        await engine.SaveSnapshotAsync(DateTime.UtcNow, 30, new EngagementMetrics { ActiveMembers = 12 }, new HealthResult(55, "NeedsAttention", [], ""));

        snapshots.Verify(r => r.Detach(It.IsAny<CommunityHealthSnapshot>()), Times.Once);   // the rejected row is not left to fail the next save
        snapshots.Verify(r => r.UpdateAsync(winner), Times.Once);
        Assert.Equal((55, 12), (winner.Score, winner.ActiveMembers));
    }

    [Fact]
    public async Task A_suggestion_another_request_already_raised_is_skipped_and_the_rest_are_still_saved()
    {
        var recs = new Mock<IAlumniPgRepository<EngagementRecommendation>>();
        var realRecs = new AlumniPgRepository<EngagementRecommendation>(TestDb.Create(TestDb.NewName(), Tenant));
        recs.Setup(r => r.GetQueryable(It.IsAny<System.Linq.Expressions.Expression<Func<EngagementRecommendation, bool>>>(), It.IsAny<bool>())).Returns<System.Linq.Expressions.Expression<Func<EngagementRecommendation, bool>>, bool>((p, i) => realRecs.GetQueryable(p, i));
        var saved = new List<string>();
        recs.Setup(r => r.AddAsync(It.IsAny<EngagementRecommendation>())).Returns<EngagementRecommendation>(row =>
        {
            if (row.RuleId == "welcome-new-members") throw new Microsoft.EntityFrameworkCore.DbUpdateException("conflict", UniqueViolation());
            saved.Add(row.RuleId); return Task.FromResult(1);
        });
        var engine = EngineWith(Mock.Of<IAlumniPgRepository<CommunityHealthSnapshot>>(), recs.Object);

        await engine.SyncRecommendationsAsync(new EngagementMetrics { Now = DateTime.UtcNow, ActiveMembers = 50, JoinedLast7Days = 4, PendingMembers = 2, OldestPendingDays = 8 }, new HashSet<string>(), DateTime.UtcNow);

        Assert.Contains("pending-members", saved);
        recs.Verify(r => r.Detach(It.Is<EngagementRecommendation>(x => x.RuleId == "welcome-new-members")), Times.Once);
    }

    [Fact]
    public async Task A_failure_that_is_not_a_duplicate_is_not_swallowed()
    {
        var snapshots = new Mock<IAlumniPgRepository<CommunityHealthSnapshot>>();
        snapshots.Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CommunityHealthSnapshot, bool>>>(), It.IsAny<bool>())).ReturnsAsync((CommunityHealthSnapshot?)null);
        snapshots.Setup(r => r.AddAsync(It.IsAny<CommunityHealthSnapshot>())).ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("disk full", new InvalidOperationException()));
        var engine = EngineWith(snapshots.Object, Mock.Of<IAlumniPgRepository<EngagementRecommendation>>());

        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            engine.SaveSnapshotAsync(DateTime.UtcNow, 30, new EngagementMetrics(), new HealthResult(10, "Inactive", [], "")));
    }

    private static EngagementEngine EngineWith(IAlumniPgRepository<CommunityHealthSnapshot> snapshots, IAlumniPgRepository<EngagementRecommendation> recs)
    {
        var ctx = TestDb.Create(TestDb.NewName(), Tenant);
        return new EngagementEngine(
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<ForumThread>(ctx), new AlumniPgRepository<ForumPost>(ctx), new AlumniPgRepository<EventRsvp>(ctx),
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<ClassNote>(ctx), new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<Campaign>(ctx),
            new AlumniPgRepository<CampaignUpdate>(ctx), new AlumniPgRepository<NewsPost>(ctx), new AlumniPgRepository<Job>(ctx), snapshots, recs,
            new AlumniPgRepository<StaffEntity>(ctx), new AlumniPgRepository<StaffActivityWeek>(ctx), new AlumniPgRepository<Referral>(ctx), TestDb.Tenant(Tenant));
    }
}
