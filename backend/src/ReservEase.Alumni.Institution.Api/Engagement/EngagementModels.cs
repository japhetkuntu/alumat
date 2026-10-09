using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Institution.Api.Engagement;

public record HealthDto(int? Score, int? PreviousScore, string Classification, string Summary, IReadOnlyList<HealthFactor> Factors);

public record Comparison(int Current, int Previous);

public record EngagementDashboardDto(
    int PeriodDays,
    DateTime GeneratedAt,
    HealthDto Health,
    int ActiveMembers,
    int PendingMembers,
    Comparison NewMembers,
    int Activated,
    int SignedInLast7Days,
    int SignedInLast30Days,
    Comparison Participants,
    /// <summary>Null when the previous period had too few participants to compare against.</summary>
    int? RetentionPercent,
    int Dormant,
    decimal ContributionVolume,
    decimal PreviousContributionVolume,
    int Contributors,
    int UpcomingEvents,
    int ActiveCampaigns,
    IReadOnlyList<WeeklyPoint> Weekly,
    IReadOnlyList<RecommendationDto> Recommendations,
    IReadOnlyList<ChecklistItem> Checklist,
    string WeekStart);

public record RecommendationDto(
    string Id, string RuleId, string Title, string Explanation, string Priority, string ActionLabel, string ActionUrl,
    string Status, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? SnoozedUntil, DateTime? ResolvedAt, string? ResolvedByName,
    string? AssignedToId, string? AssignedToName)
{
    public static RecommendationDto From(EngagementRecommendation r) => new(
        r.Id, r.RuleId, r.Title, r.Explanation, r.Priority, r.ActionLabel, r.ActionUrl, r.Status, r.CreatedAt, r.ExpiresAt,
        r.SnoozedUntil, r.ResolvedAt, r.ResolvedByName, r.AssignedToId, r.AssignedToName);
}

public record HealthSnapshotDto(DateTime Date, int? Score, string Classification, int ActiveMembers, IReadOnlyList<HealthFactor> Factors);

public record ResolveRecommendationRequest(string Action, int? SnoozeDays);
public record AssignRecommendationRequest(string? StaffId);
public record ChecklistToggleRequest(bool Done);

/// <summary>
/// A month's report. Activity (what was done) and outcomes (what members did) are separate lists on purpose. The standing
/// section (health factors, year groups, ambassadors) is the position now, not at the end of the month, and says so.
/// </summary>
public record MonthlyReportDto(
    string Month, bool IsCurrentMonth, MonthFigures Current, MonthFigures Previous,
    HealthDto HealthNow, IReadOnlyList<HealthFactor> NeedsAttention,
    IReadOnlyList<RecommendationDto> NextActions,
    IReadOnlyList<CohortRow> Cohorts, IReadOnlyList<AmbassadorRow> Ambassadors);
