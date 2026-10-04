namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>
/// Everything the Analytics page shows, in one response. Each part answers one question an alumni
/// office actually asks — is the community growing, are members showing up, are dues being paid,
/// where does the money come from, who are our members, what happened lately — rather than being
/// a wall of totals. All figures are cut to the requesting admin's scope.
/// </summary>
public record InstitutionAnalyticsDto(
    AnalyticsMembersDto Members,
    List<AnalyticsMonthCountDto> Growth,
    AnalyticsDuesDto? Dues,
    AnalyticsMoneyDto? Money,
    AnalyticsCompositionDto Composition,
    AnalyticsActivityDto Activity);

/// <param name="Approved">Members whose account is approved — the base every participation figure is a share of.</param>
/// <param name="SignedInEver">Approved members who have signed in at least once.</param>
/// <param name="SignedInLast30Days">Approved members who signed in within the last 30 days.</param>
public record AnalyticsMembersDto(int Total, int Approved, int Pending, int SignedInEver, int SignedInLast30Days);

/// <summary>One calendar month of members joining. <paramref name="Total"/> is the running membership at the end of that month.</summary>
public record AnalyticsMonthCountDto(int Year, int Month, int Count, int Total);

/// <summary>Standing on one membership year's dues. Null on the parent when no dues were set for the year, or Contributions is switched off.</summary>
/// <param name="Eligible">Approved members the dues fall on.</param>
/// <param name="ByYearGroup">Per graduation year, for year groups with anyone eligible. Empty for institutions that don't collect graduation years.</param>
public record AnalyticsDuesDto(int Year, int Eligible, int Paid, decimal Collected, List<AnalyticsDuesYearGroupDto> ByYearGroup);

public record AnalyticsDuesYearGroupDto(int YearGroup, int Eligible, int Paid);

/// <summary>Money received. Null on the parent when no money feature is switched on.</summary>
/// <param name="ThisYear">Received so far this calendar year.</param>
/// <param name="LastYearToDate">Received by this same date last year — a like-for-like comparison, not last year's full total.</param>
/// <param name="PayersThisYear">Different members who made a successful contribution this year.</param>
public record AnalyticsMoneyDto(decimal ThisYear, decimal LastYearToDate, int PayersThisYear, List<AnalyticsMoneyMonthDto> Months);

public record AnalyticsMoneyMonthDto(int Year, int Month, decimal Contributions, decimal Store, decimal Services);

/// <summary>Who the approved members are. Each list is largest first; departments and locations keep the top few.</summary>
public record AnalyticsCompositionDto(List<AnalyticsSliceDto> ByYearGroup, List<AnalyticsSliceDto> ByDepartment, List<AnalyticsSliceDto> ByLocation, int WithoutLocation);

public record AnalyticsSliceDto(string Label, int Count);

/// <summary>What members did in the last 30 days, each beside the 30 days before for comparison.</summary>
public record AnalyticsActivityDto(AnalyticsChangeDto NewMembers, AnalyticsChangeDto Payments, AnalyticsChangeDto EventSignUps, AnalyticsChangeDto ForumPosts);

public record AnalyticsChangeDto(int Last30Days, int Previous30Days);
