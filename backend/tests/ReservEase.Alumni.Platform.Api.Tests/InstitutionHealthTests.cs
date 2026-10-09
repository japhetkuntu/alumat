using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Platform.Api.Tests;

public class InstitutionHealthAssessmentTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static InstitutionHealthFacts Facts(int ageDays = 90, string? classification = "Healthy", int? score = 80, int? weekAgo = 80, int members = 100,
        int? adminAwayDays = 2, int suggestions = 2, int overdue = 0)
        => new(Now.AddDays(-ageDays), Now, classification, score, weekAgo, members, adminAwayDays, suggestions, overdue);

    [Fact]
    public void A_healthy_established_institution_is_on_track_with_no_reasons()
    {
        var (status, reasons) = InstitutionHealthAssessment.Assess(Facts());

        Assert.Equal(InstitutionHealthStatuses.OnTrack, status);
        Assert.Empty(reasons);
    }

    [Fact]
    public void A_brand_new_institution_is_never_flagged_however_empty_it_looks()
    {
        var (status, reasons) = InstitutionHealthAssessment.Assess(Facts(ageDays: 5, classification: null, score: null, weekAgo: null, members: 0, adminAwayDays: null));

        Assert.Equal(InstitutionHealthStatuses.Onboarding, status);
        Assert.Empty(reasons);
    }

    [Theory]
    [InlineData("AtRisk", 30, "at risk")]
    [InlineData("Inactive", 12, "very low")]
    public void A_low_reading_is_flagged_with_the_score_in_the_reason(string classification, int score, string phrase)
    {
        var (status, reasons) = InstitutionHealthAssessment.Assess(Facts(classification: classification, score: score, weekAgo: score));

        Assert.Equal(InstitutionHealthStatuses.NeedsAttention, status);
        Assert.Contains(reasons, r => r.Contains(phrase) && r.Contains($"{score}/100"));
    }

    [Fact]
    public void A_sharp_fall_in_a_week_is_flagged_even_while_still_healthy_but_a_small_one_is_not()
    {
        Assert.Contains("fell 15 points", Assert.Single(InstitutionHealthAssessment.Assess(Facts(score: 70, weekAgo: 85)).Reasons));
        Assert.Equal(InstitutionHealthStatuses.OnTrack, InstitutionHealthAssessment.Assess(Facts(score: 78, weekAgo: 85)).Status);
    }

    [Fact]
    public void Administrators_away_three_weeks_are_flagged_and_two_weeks_is_not()
    {
        Assert.Equal(InstitutionHealthStatuses.NeedsAttention, InstitutionHealthAssessment.Assess(Facts(adminAwayDays: 21)).Status);
        Assert.Equal(InstitutionHealthStatuses.OnTrack, InstitutionHealthAssessment.Assess(Facts(adminAwayDays: 14)).Status);
    }

    [Fact]
    public void An_established_institution_whose_administrators_never_signed_in_is_flagged()
        => Assert.Contains(InstitutionHealthAssessment.Assess(Facts(adminAwayDays: null)).Reasons, r => r.Contains("never") || r.Contains("not signed in") || r.Contains("signed in yet"));

    [Fact]
    public void A_pile_of_waiting_suggestions_and_overdue_follow_ups_are_flagged()
    {
        var (_, reasons) = InstitutionHealthAssessment.Assess(Facts(suggestions: 9, overdue: 2));

        Assert.Contains(reasons, r => r.Contains("9 suggested actions"));
        Assert.Contains(reasons, r => r.Contains("2 follow-up tasks are overdue"));
    }

    [Fact]
    public void Still_too_few_members_after_a_month_is_flagged_but_not_at_ten_days()
    {
        Assert.Contains(InstitutionHealthAssessment.Assess(Facts(ageDays: 45, classification: "InsufficientData", score: null, weekAgo: null, members: 2)).Reasons, r => r.Contains("Only 2 active members"));
        Assert.Equal(InstitutionHealthStatuses.Onboarding, InstitutionHealthAssessment.Assess(Facts(ageDays: 10, classification: "InsufficientData", score: null, weekAgo: null, members: 2)).Status);
    }
}

public class InstitutionHealthServiceTests
{
    private static InstitutionHealthService Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        return new InstitutionHealthService(new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<CommunityHealthSnapshot>(ctx),
            new AlumniPgRepository<EngagementRecommendation>(ctx), new AlumniPgRepository<StaffEntity>(ctx), new AlumniPgRepository<StaffActivityWeek>(ctx),
            new AlumniPgRepository<ActivationTask>(ctx), NullLogger<InstitutionHealthService>.Instance);
    }

    private static readonly DateTime Today = DateTime.UtcNow.Date;
    private static Institution School(string id, int ageDays = 90, string status = "Active") => new() { Id = id, Name = id.ToUpper(), Slug = id, Status = status, CreatedAt = DateTime.UtcNow.AddDays(-ageDays) };
    private static CommunityHealthSnapshot Reading(string inst, int daysAgo, int? score, string cls, int members = 50) =>
        new() { InstitutionId = inst, PeriodDays = 30, SnapshotDate = Today.AddDays(-daysAgo), Score = score, Classification = cls, ActiveMembers = members };
    private static StaffEntity Admin(string inst, string id, int lastLoginDaysAgo, string role = "SuperAdmin", bool disabled = false) =>
        new() { Id = id, InstitutionId = inst, FirstName = "A", LastName = id, Email = $"{id}@x.com", Role = role, IsDisabled = disabled, LastLoginAt = DateTime.UtcNow.AddDays(-lastLoginDaysAgo) };

    [Fact]
    public async Task Every_live_institution_appears_with_its_latest_reading_and_the_weekly_change_and_the_strugglers_come_first()
    {
        var s = Create(School("good"), School("weak"), School("closed", status: "Suspended"),
            Reading("good", 0, 80, "Healthy"), Reading("good", 8, 78, "Healthy"), Reading("weak", 0, 30, "AtRisk"), Reading("weak", 9, 45, "NeedsAttention"),
            Admin("good", "g1", 1), Admin("weak", "w1", 40));

        var r = (await s.GetAsync()).Data!;

        Assert.Equal(["weak", "good"], r.Institutions.Select(i => i.Slug));
        var weak = r.Institutions[0];
        Assert.Equal((30, -15, "NeedsAttention"), (weak.Score, weak.ScoreChange, weak.Status));
        Assert.Contains(weak.Reasons, x => x.Contains("40 days"));
        Assert.Equal(2, r.Summary.Institutions);
        Assert.Equal((1, 1, 55.0), (r.Summary.NeedsAttention, r.Summary.Healthy, r.Summary.AverageScore));
    }

    [Fact]
    public async Task Open_suggestions_follow_ups_and_ambassadors_are_counted_per_institution_only()
    {
        var s = Create(School("a"), School("b"), Reading("a", 0, 70, "NeedsAttention"), Reading("b", 0, 70, "NeedsAttention"),
            Admin("a", "a1", 1), Admin("a", "amb", 1, role: "ScopedAdmin") , Admin("b", "b1", 1),
            new EngagementRecommendation { InstitutionId = "a", DedupeKey = "1", Title = "t", Status = RecommendationStatuses.Open },
            new EngagementRecommendation { InstitutionId = "a", DedupeKey = "2", Title = "t", Status = RecommendationStatuses.Completed },
            new ActivationTask { TargetId = "t", Title = "Call a", AssigneeId = "p", InstitutionId = "a", Status = TaskStatuses.Todo, DueDate = DateTime.UtcNow.AddDays(-2) },
            new ActivationTask { TargetId = "t", Title = "Done", AssigneeId = "p", InstitutionId = "a", Status = TaskStatuses.Done });

        var rows = (await s.GetAsync()).Data!.Institutions.ToDictionary(i => i.Slug);

        Assert.Equal((1, 1, 1), (rows["a"].OpenSuggestions, rows["a"].OpenFollowUps, rows["a"].OverdueFollowUps));
        Assert.Equal((0, 0, 0), (rows["b"].OpenSuggestions, rows["b"].OpenFollowUps, rows["b"].Ambassadors));
    }

    [Fact]
    public async Task A_disabled_administrator_does_not_count_as_activity_and_an_institution_with_no_readings_still_lists()
    {
        var s = Create(School("quiet"), Admin("quiet", "off", 1, disabled: true));

        var row = Assert.Single((await s.GetAsync()).Data!.Institutions);

        Assert.Null(row.Score);
        Assert.Null(row.DaysSinceAdminActive);
        Assert.Contains(row.Reasons, r => r.Contains("No health reading"));
        Assert.Equal(1, (await s.GetAsync()).Data!.Summary.NoReading);
    }

    [Fact]
    public void The_overview_carries_no_member_or_payment_level_fields_at_all()
    {
        var names = typeof(InstitutionHealthRow).GetProperties().Select(p => p.Name).Concat(typeof(InstitutionHealthSummary).GetProperties().Select(p => p.Name));

        Assert.DoesNotContain(names, n => n.Contains("Email") || n.Contains("Member", StringComparison.Ordinal) && n != "ActiveMembers" || n.Contains("Phone") || n.Contains("Amount") || n.Contains("Revenue"));
    }
}
