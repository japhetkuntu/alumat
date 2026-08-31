namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>One settlement window's figure — an estimate from confirmed transactions, not a Paystack-confirmed settlement. Same shape institutions see on their own side, so the two views can never disagree.</summary>
public record PayoutWindowDto(DateOnly Date, decimal Amount, int TransactionCount);

public record InstitutionPayoutForecast(
    string InstitutionId,
    string InstitutionName,
    bool PayoutsConfigured,
    PayoutWindowDto LastPayout,
    PayoutWindowDto NextPayout);

public record PayoutForecastTotals(PayoutWindowDto LastPayout, PayoutWindowDto NextPayout);

public record PlatformPayoutForecastResponse(
    PayoutForecastTotals Totals,
    List<InstitutionPayoutForecast> Institutions);
