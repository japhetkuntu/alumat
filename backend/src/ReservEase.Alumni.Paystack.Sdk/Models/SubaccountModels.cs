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

/// <summary>One entry from Paystack's List Banks endpoint — covers both real banks (type "ghipss") and mobile money providers (type "mobile_money").</summary>
public class PaystackBank
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("currency")]
    public string Currency { get; set; } = string.Empty;
}

public class ListBanksResponse
{
    [JsonProperty("status")]
    public bool Status { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("data")]
    public List<PaystackBank> Data { get; set; } = [];
}

public class ResolveAccountResponse
{
    [JsonProperty("status")]
    public bool Status { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("data")]
    public ResolveAccountData? Data { get; set; }
}

public class ResolveAccountData
{
    [JsonProperty("account_number")]
    public string? AccountNumber { get; set; }

    [JsonProperty("account_name")]
    public string? AccountName { get; set; }
}
