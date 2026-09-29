using System.ComponentModel.DataAnnotations;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Models;

public record ActivationScorecardItem(
    string InstitutionId, string Name, string Slug, DateTime OnboardedAt, DateTime? ActivatedAt, int DaysLive,
    List<ActivationCriterion> Criteria, int MetCount, string? NextStep, bool IsStalled, bool IsActivated,
    bool IsOverdue, int MinMembers, DateTime? TrialEndsAt, bool SetupNudgesEnabled);

/// <summary>
/// A milestone with its outcome. Past milestones are judged on the counts as of
/// their date; future ones on today's counts. Status: "met", "missed" or "open".
/// </summary>
public record MilestoneProgress(
    DateTime Date, int? LiveTarget, int? ActivatedTarget, int LiveActual, int ActivatedActual, string Status);

public record ActivationScorecardResponse(
    int LiveCount, int ActivatedCount, int StalledCount, int OverdueCount,
    int? TargetCount, DateTime? TargetDate,
    List<MilestoneProgress> Milestones,
    List<ActivationScorecardItem> Items);

public record FunnelStage(string Key, string Label, int Count);

/// <summary>How many leads/institutions first reached each stage during the week starting <see cref="WeekStart"/>.</summary>
public record FunnelWeek(DateTime WeekStart, int Leads, int Contacted, int Live);

public record ActivationFunnelResponse(List<FunnelStage> Stages, List<FunnelWeek> Weeks);

public record ActivationMilestoneRequest(DateTime Date, int? LiveTarget, int? ActivatedTarget);

public record UpdateActivationTargetRequest(int? TargetCount, DateTime? TargetDate, List<ActivationMilestoneRequest>? Milestones = null);

public class UpdateInstitutionActivationSettingsRequest
{
    /// <summary>Member threshold for this institution; null returns it to the platform default.</summary>
    [Range(1, 1_000_000)]
    public int? ActivationMinMembers { get; set; }
    public bool SetupNudgesEnabled { get; set; } = true;
}
