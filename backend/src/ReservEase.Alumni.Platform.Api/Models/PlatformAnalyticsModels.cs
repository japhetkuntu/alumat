namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>
/// The platform Analytics page in one response: are we gaining institutions and are they alive, are
/// their members showing up, is money moving and what do we earn from it, who is carrying the
/// platform, and who has gone quiet and needs a call.
/// </summary>
/// <param name="Money">Null for roles that don't see revenue.</param>
/// <param name="Leaders">Empty for roles that don't see revenue.</param>
public record PlatformAnalyticsDto(
    PlatformAnalyticsInstitutionsDto Institutions,
    PlatformAnalyticsMembersDto Members,
    List<PlatformAnalyticsMonthCountDto> InstitutionGrowth,
    List<PlatformAnalyticsMonthCountDto> MemberGrowth,
    PlatformAnalyticsMoneyDto? Money,
    List<PlatformAnalyticsLeaderDto> Leaders,
    List<PlatformAnalyticsQuietDto> Quiet);

/// <param name="Activated">Institutions that reached activation (see InstitutionActivationService).</param>
/// <param name="Collecting">Institutions that received at least one successful payment in the last 90 days.</param>
/// <param name="WithRecentSignIns">Institutions where at least one member signed in during the last 30 days.</param>
public record PlatformAnalyticsInstitutionsDto(int Total, int Activated, int Collecting, int WithRecentSignIns);

public record PlatformAnalyticsMembersDto(int Total, int Approved, int SignedInLast30Days);

/// <summary>One calendar month of arrivals. <paramref name="Total"/> is the running total at the end of that month.</summary>
public record PlatformAnalyticsMonthCountDto(int Year, int Month, int Count, int Total);

/// <param name="Collected">What institutions received.</param>
/// <param name="Earned">What the platform earned on it.</param>
/// <param name="LastYearToDateCollected">By this same date last year — like for like, not last year's full total.</param>
public record PlatformAnalyticsMoneyDto(
    decimal ThisYearCollected, decimal LastYearToDateCollected, decimal ThisYearEarned, decimal LastYearToDateEarned,
    List<PlatformAnalyticsMoneyMonthDto> Months);

public record PlatformAnalyticsMoneyMonthDto(int Year, int Month, decimal Collected, decimal Earned);

/// <summary>An institution ranked by what it collected in the last 90 days.</summary>
public record PlatformAnalyticsLeaderDto(string InstitutionId, string Name, decimal Collected, decimal Earned, int Members);

/// <summary>An institution with members where nobody has signed in for 30 days. Null dates mean "never".</summary>
public record PlatformAnalyticsQuietDto(string InstitutionId, string Name, int Members, DateTime? LastSignIn, DateTime? LastPayment);
