namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>One settlement window's figure — an estimate from our own confirmed transactions, not a Paystack-confirmed settlement.</summary>
public record PayoutWindowDto(DateOnly Date, decimal Amount, int TransactionCount);

public record PayoutForecastResponse(
    bool PayoutsConfigured,
    PayoutWindowDto LastPayout,
    PayoutWindowDto NextPayout);

/// <summary>How much of one day's payout came from one kind of payment.</summary>
public record PayoutSourceTotalDto(string Source, decimal Amount, int Count);

/// <summary>One past payout day: what was paid into the bank account that morning, and from what.</summary>
public record PayoutDayDto(DateOnly Date, decimal Amount, int TransactionCount, List<PayoutSourceTotalDto> Sources, List<PayoutAccountTotalDto> Accounts);

/// <summary>One settlement account an institution is paid into: its own main account, or a year group's dedicated one.</summary>
public record PayoutAccountDto(string Key, string Name, string? Detail);

/// <summary>How much of one day's payouts went into one account.</summary>
public record PayoutAccountTotalDto(string Key, string Name, decimal Amount, int Count);

/// <summary>
/// Past payout days, newest first, only the days money was paid out. <paramref name="HasOlder"/> says whether going back
/// further would find more. Like the forecast, an estimate from our own confirmed payments, not a bank statement.
/// </summary>
public record PayoutHistoryResponse(bool PayoutsConfigured, List<PayoutAccountDto> AccountOptions, string Account, List<PayoutDayDto> Days, decimal TotalAmount, bool HasOlder, DateOnly From, DateOnly To);

/// <summary>One payment inside a payout day.</summary>
public record PayoutItemDto(string Source, string Account, string Title, string? PayerName, decimal Amount, DateTime ConfirmedAt);

public record PayoutDayDetailDto(DateOnly Date, decimal Amount, List<PayoutItemDto> Items);
