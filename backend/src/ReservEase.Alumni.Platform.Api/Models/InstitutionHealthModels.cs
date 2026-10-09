namespace ReservEase.Alumni.Platform.Api.Models;

public static class InstitutionHealthStatuses
{
    /// <summary>Too new to judge. Not flagged: a community a week old is not failing.</summary>
    public const string Onboarding = "Onboarding";
    public const string OnTrack = "OnTrack";
    public const string NeedsAttention = "NeedsAttention";
}

/// <summary>What the assessment is worked out from. Aggregates per institution only: no member or payment detail.</summary>
public record InstitutionHealthFacts(
    DateTime CreatedAt, DateTime Now, string? Classification, int? Score, int? ScoreWeekAgo, int ActiveMembers,
    int? DaysSinceAdminActive, int OpenSuggestions, int OverdueFollowUps);

public record InstitutionHealthRow(
    string InstitutionId, string Name, string Slug, DateTime CreatedAt, string Status, IReadOnlyList<string> Reasons,
    string? Classification, int? Score, int? ScoreChange, int ActiveMembers, int? DaysSinceAdminActive,
    int OpenSuggestions, int Ambassadors, int OpenFollowUps, int OverdueFollowUps);

public record InstitutionHealthSummary(
    int Institutions, int NeedsAttention, int OnTrack, int Onboarding, int Healthy, int AtRiskOrInactive, int NoReading,
    int WithAmbassadors, double? AverageScore);

public record InstitutionHealthDto(InstitutionHealthSummary Summary, IReadOnlyList<InstitutionHealthRow> Institutions);
