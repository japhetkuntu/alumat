namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>One settlement window's figure — an estimate from confirmed transactions, not a Paystack-confirmed settlement. Same shape institutions see on their own side, so the two views can never disagree.</summary>
public record PayoutWindowDto(DateOnly Date, decimal Amount, int TransactionCount);

public record InstitutionPayoutForecast(
    string InstitutionId,
    string InstitutionName,
    bool PayoutsConfigured,
    PayoutWindowDto LastPayout,
    PayoutWindowDto NextPayout,
    // Money in the same windows that is paid straight into a group's or year group's own account instead of the institution's. It is
    // not part of LastPayout/NextPayout, but it is collected money all the same, so the platform sees it here.
    PayoutWindowDto? LastOtherAccounts = null,
    PayoutWindowDto? NextOtherAccounts = null);

public record PayoutForecastTotals(PayoutWindowDto LastPayout, PayoutWindowDto NextPayout, PayoutWindowDto? LastOtherAccounts = null, PayoutWindowDto? NextOtherAccounts = null);

public record PlatformPayoutForecastResponse(
    PayoutForecastTotals Totals,
    List<InstitutionPayoutForecast> Institutions);
