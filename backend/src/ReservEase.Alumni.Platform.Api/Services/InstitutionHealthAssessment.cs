using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services;

/// <summary>
/// Decides which institutions need a person from AlumUnion to reach out, and says why in plain words. Every reason is a fact
/// from the institution's own readings, so staff can read it back to the institution. A new institution is never flagged for
/// being new, and an institution is never flagged for a single quiet week.
/// </summary>
public static class InstitutionHealthAssessment
{
    public const int OnboardingDays = 14;
    public const int AdminAwayDays = 21;
    public const int ScoreDropPoints = 10;
    public const int SuggestionBacklog = 8;

    public static (string Status, IReadOnlyList<string> Reasons) Assess(InstitutionHealthFacts f)
    {
        var age = (f.Now - f.CreatedAt).TotalDays;
        var reasons = new List<string>();

        if (f.Classification is "AtRisk" or "Inactive")
            reasons.Add(f.Classification == "Inactive" ? $"Community health is very low ({f.Score}/100)" : $"Community health is at risk ({f.Score}/100)");
        if (f.Score is { } now && f.ScoreWeekAgo is { } before && before - now >= ScoreDropPoints)
            reasons.Add($"Health score fell {before - now} points in a week");
        if (f.DaysSinceAdminActive is { } away && away >= AdminAwayDays)
            reasons.Add($"No administrator has been active for {away} days");
        else if (f.DaysSinceAdminActive is null && age > OnboardingDays)
            reasons.Add("No administrator has signed in yet");
        if (f.OpenSuggestions >= SuggestionBacklog)
            reasons.Add($"{f.OpenSuggestions} suggested actions are waiting");
        if (f.OverdueFollowUps > 0)
            reasons.Add($"{f.OverdueFollowUps} follow-up {(f.OverdueFollowUps == 1 ? "task is" : "tasks are")} overdue");
        if (f.Classification == "InsufficientData" && age > 30)
            reasons.Add($"Only {f.ActiveMembers} active {(f.ActiveMembers == 1 ? "member" : "members")} a month after joining");
        if (f.Classification is null && age > OnboardingDays)
            reasons.Add("No health reading has been recorded");

        if (reasons.Count > 0 && age > OnboardingDays) return (InstitutionHealthStatuses.NeedsAttention, reasons);
        if (age <= OnboardingDays) return (InstitutionHealthStatuses.Onboarding, []);
        return (InstitutionHealthStatuses.OnTrack, []);
    }
}
