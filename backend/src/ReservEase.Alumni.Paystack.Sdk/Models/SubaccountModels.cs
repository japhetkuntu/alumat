using Newtonsoft.Json;

namespace ReservEase.Alumni.Paystack.Sdk.Models;

/// <summary>
/// Fields to create or update a Paystack subaccount for an institution's
/// settlement account. <see cref="PercentageCharge"/> is the MAIN (platform)
/// account's cut of each split transaction — e.g. 5 means the platform keeps
/// 5% and the subaccount (institution) receives the remaining 95%. Confirmed
/// against Paystack's Subaccount API docs, not assumed.
/// </summary>
public class SubaccountRequest
{
    public string BusinessName { get; set; } = string.Empty;
    public string SettlementBank { get; set; } = string.Empty; // bank code, not display name
    public string AccountNumber { get; set; } = string.Empty;
    public decimal PercentageCharge { get; set; }
}

public class SubaccountResponse
{
    [JsonProperty("status")]
    public bool Status { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("data")]
    public SubaccountData? Data { get; set; }
}

public class SubaccountData
{
    [JsonProperty("subaccount_code")]
    public string? SubaccountCode { get; set; }

    [JsonProperty("business_name")]
    public string? BusinessName { get; set; }

    [JsonProperty("account_number")]
    public string? AccountNumber { get; set; }

    [JsonProperty("settlement_bank")]
    public string? SettlementBank { get; set; }

    [JsonProperty("percentage_charge")]
    public decimal PercentageCharge { get; set; }

    [JsonProperty("active")]
    public bool Active { get; set; }
}
