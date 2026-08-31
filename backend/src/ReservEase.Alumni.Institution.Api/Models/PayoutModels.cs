namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>One settlement window's figure — an estimate from our own confirmed transactions, not a Paystack-confirmed settlement.</summary>
public record PayoutWindowDto(DateOnly Date, decimal Amount, int TransactionCount);

public record PayoutForecastResponse(
    bool PayoutsConfigured,
    PayoutWindowDto LastPayout,
    PayoutWindowDto NextPayout);
