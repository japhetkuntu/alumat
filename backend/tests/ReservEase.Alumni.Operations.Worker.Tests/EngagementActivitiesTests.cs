using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Operations.Worker.Tests;

public class EngagementActivitiesTests
{
    private const string Inst = "inst-1";
    private const string Other = "inst-2";

    private sealed class FixedClock(DateTime utc) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero); }

    private static DateTime At(int hour) => new(2026, 10, 9, hour, 0, 0, DateTimeKind.Utc);

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CurrentTenantService Tenant { get; } = new();
        public EngagementActivities Activities { get; }

        public Rig(DateTime now)
        {
            var db = TestDb.Create(DbName, tenant: Tenant);
            AlumniPgRepository<T> R<T>() where T : BaseEntity => new(db);
            var engine = new EngagementEngine(R<MemberEntity>(), R<ForumThread>(), R<ForumPost>(), R<EventRsvp>(), R<Contribution>(), R<ClassNote>(), R<AlumniEvent>(), R<Campaign>(),
                R<CampaignUpdate>(), R<NewsPost>(), R<Job>(), R<CommunityHealthSnapshot>(), R<EngagementRecommendation>(), R<StaffEntity>(), R<StaffActivityWeek>(), R<Referral>(), Tenant);
            Activities = new EngagementActivities(R<Institution>(), R<MemberEntity>(), R<StaffEntity>(), R<StaffActivityWeek>(), R<EngagementRecommendation>(), R<EngagementMessage>(),
                R<NotificationPreference>(), R<AlumniEvent>(), R<Job>(), R<NewsPost>(), R<ForumThread>(), R<ForumPost>(), R<EventRsvp>(), R<Contribution>(), R<ClassNote>(),
                engine, Tenant, Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MemberPortalBaseDomain"] = "members.test", ["AdminPortalBaseDomain"] = "admin.test" }).Build(),
                NullLogger<EngagementActivities>.Instance) { Clock = new FixedClock(now) };
        }

        public AlumniDbContext Db() => TestDb.Create(DbName);
        public async Task Seed(params object[] entities) { using var db = Db(); foreach (var e in entities) db.Add(e); await db.SaveChangesAsync(); }
        public List<EngagementMessage> Messages() { using var db = Db(); return db.Set<EngagementMessage>().IgnoreQueryFilters().AsNoTracking().ToList(); }
    }

    private static Institution School(string id = Inst, bool email = true) => new() { Id = id, Slug = id, Name = id, Status = "Active", EmailNotificationsEnabled = email };
    private static StaffEntity Admin(string id, DateTime? lastLogin, string tenant = Inst, bool disabled = false) =>
        new() { Id = id, InstitutionId = tenant, FirstName = "A", LastName = id, Email = $"{id}@x.com", Role = "SuperAdmin", LastLoginAt = lastLogin, IsDisabled = disabled };
    private static MemberEntity Member(string id, DateTime now, int joinedDaysAgo = 200, int? lastLoginDaysAgo = 90, string tenant = Inst, int year = 2014) =>
        new() { Id = id, InstitutionId = tenant, Status = "Active", FirstName = id, LastName = "M", Email = $"{id}@x.com", GraduationYear = year,
                CreatedAt = now.AddDays(-joinedDaysAgo), LastLoginAt = lastLoginDaysAgo is null ? null : now.AddDays(-lastLoginDaysAgo.Value) };
    private static AlumniEvent Soon(DateTime now, string tenant = Inst) => new() { InstitutionId = tenant, Title = "Homecoming", Status = "Upcoming", StartDate = now.AddDays(5) };

    // ── Refresh ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_daily_refresh_records_a_reading_for_a_community_nobody_has_opened_and_is_safe_to_repeat()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), Member("a", now, lastLoginDaysAgo: 2), Member("b", now, lastLoginDaysAgo: 3), Member("c", now, lastLoginDaysAgo: 4), Member("d", now, lastLoginDaysAgo: 1), Member("e", now, lastLoginDaysAgo: 1));

        await rig.Activities.RefreshInstitutionAsync(Inst);
        await rig.Activities.RefreshInstitutionAsync(Inst);

        using var db = rig.Db();
        Assert.Equal(1, db.Set<CommunityHealthSnapshot>().IgnoreQueryFilters().Count(s => s.InstitutionId == Inst));
    }

    [Fact]
    public async Task The_refresh_only_ever_writes_for_the_institution_it_was_asked_about()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), School(Other), Member("a", now, lastLoginDaysAgo: 2), Member("x", now, tenant: Other, lastLoginDaysAgo: 2));

        await rig.Activities.RefreshInstitutionAsync(Inst);

        using var db = rig.Db();
        Assert.All(db.Set<CommunityHealthSnapshot>().IgnoreQueryFilters().ToList(), s => Assert.Equal(Inst, s.InstitutionId));
        Assert.Empty(db.Set<CommunityHealthSnapshot>().IgnoreQueryFilters().Where(s => s.InstitutionId == Other));
    }

    // ── Administrators ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_administrator_away_for_weeks_is_reminded_once_in_the_daytime_and_not_again_on_the_next_run()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), Admin("away", now.AddDays(-30)), new EngagementRecommendation { InstitutionId = Inst, DedupeKey = "k", Title = "T", Status = RecommendationStatuses.Open });

        var first = await rig.Activities.SendAdminMessagesAsync(Inst);
        var second = await rig.Activities.SendAdminMessagesAsync(Inst);

        Assert.Equal((1, 0), (first, second));
        var m = Assert.Single(rig.Messages());
        Assert.Equal(("away", EngagementMessageKinds.AdminReminder), (m.RecipientId, m.Kind));
    }

    [Fact]
    public async Task Outside_the_daytime_window_nothing_is_sent_or_recorded()
    {
        var rig = new Rig(At(2));
        await rig.Seed(School(), Admin("away", At(2).AddDays(-30)), new EngagementRecommendation { InstitutionId = Inst, DedupeKey = "k", Title = "T", Status = RecommendationStatuses.Open });

        Assert.Equal(0, await rig.Activities.SendAdminMessagesAsync(Inst));
        Assert.Empty(rig.Messages());
    }

    [Fact]
    public async Task A_disabled_administrator_and_other_institutions_administrators_are_never_contacted()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), School(Other), Admin("off", now.AddDays(-60), disabled: true), Admin("elsewhere", now.AddDays(-60), tenant: Other),
            new EngagementRecommendation { InstitutionId = Inst, DedupeKey = "k", Title = "T", Status = RecommendationStatuses.Open });

        Assert.Equal(0, await rig.Activities.SendAdminMessagesAsync(Inst));
    }

    [Fact]
    public async Task A_colleague_away_a_long_time_is_reported_to_the_administrator_who_is_still_active()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), Admin("away", now.AddDays(-45)), Admin("here", now.AddDays(-1)), new EngagementRecommendation { InstitutionId = Inst, DedupeKey = "k", Title = "T", Status = RecommendationStatuses.Open });

        await rig.Activities.SendAdminMessagesAsync(Inst);

        var sent = rig.Messages();
        Assert.Contains(sent, x => x.RecipientId == "here" && x.Kind == EngagementMessageKinds.AdminEscalation && x.Subject == "away");
        Assert.Contains(sent, x => x.RecipientId == "away" && x.Kind == EngagementMessageKinds.AdminReminder);
    }

    // ── Members ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_quiet_member_hears_from_us_only_when_there_is_something_real_and_only_once()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), Member("quiet", now), Soon(now));

        var first = await rig.Activities.SendMemberReengagementAsync(Inst);
        var second = await rig.Activities.SendMemberReengagementAsync(Inst);

        Assert.Equal((1, 0), (first, second));
        Assert.Equal(["quiet"], rig.Messages().Select(m => m.RecipientId));
    }

    [Fact]
    public async Task With_nothing_to_show_nobody_is_written_to()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), Member("quiet", now));

        Assert.Equal(0, await rig.Activities.SendMemberReengagementAsync(Inst));
        Assert.Empty(rig.Messages());
    }

    [Fact]
    public async Task Members_who_opted_out_or_are_active_or_belong_elsewhere_are_not_written_to()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), School(Other),
            Member("optout", now), new NotificationPreference { InstitutionId = Inst, MemberId = "optout", EngagementMessages = false },
            Member("around", now, lastLoginDaysAgo: 3),
            Member("poster", now), new ForumThread { InstitutionId = Inst, AuthorId = "poster", Title = "Hi", CategoryId = "g", CreatedAt = now.AddDays(-5) },
            Member("newbie", now, joinedDaysAgo: 5, lastLoginDaysAgo: null),
            Member("elsewhere", now, tenant: Other),
            Soon(now));

        Assert.Equal(0, await rig.Activities.SendMemberReengagementAsync(Inst));
    }

    [Fact]
    public async Task A_member_with_no_preference_row_is_treated_as_opted_in()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), Member("plain", now), Soon(now));

        Assert.Equal(1, await rig.Activities.SendMemberReengagementAsync(Inst));
    }

    [Fact]
    public async Task Content_aimed_at_another_year_group_does_not_count_for_a_member()
    {
        var now = At(10);
        var rig = new Rig(now);
        var forOthers = Soon(now); forOthers.YearGroups = [2019];
        await rig.Seed(School(), Member("quiet", now, year: 2014), forOthers);

        Assert.Equal(0, await rig.Activities.SendMemberReengagementAsync(Inst));
    }

    [Fact]
    public async Task One_run_never_contacts_more_members_than_the_cap()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed([School(), Soon(now), .. Enumerable.Range(0, 130).Select(i => Member($"m{i}", now))]);

        var sent = await rig.Activities.SendMemberReengagementAsync(Inst);

        Assert.Equal(new AutomationLimits().MaxMemberMessagesPerRun, sent);
    }

    [Fact]
    public async Task A_member_messaged_by_another_engagement_kind_this_week_is_left_for_later()
    {
        var now = At(10);
        var rig = new Rig(now);
        await rig.Seed(School(), Member("busy", now), Soon(now),
            new EngagementMessage { InstitutionId = Inst, RecipientType = "Member", RecipientId = "busy", Kind = "Other", CreatedAt = now.AddDays(-2) });

        Assert.Equal(0, await rig.Activities.SendMemberReengagementAsync(Inst));
    }

    [Fact]
    public async Task Nothing_is_sent_when_the_institution_has_switched_email_off_or_outside_the_window()
    {
        var now = At(10);
        var off = new Rig(now);
        await off.Seed(School(email: false), Member("quiet", now), Soon(now));
        var night = new Rig(At(3));
        await night.Seed(School(), Member("quiet", At(3)), Soon(At(3)));

        Assert.Equal(0, await off.Activities.SendMemberReengagementAsync(Inst));
        Assert.Equal(0, await night.Activities.SendMemberReengagementAsync(Inst));
    }
}
