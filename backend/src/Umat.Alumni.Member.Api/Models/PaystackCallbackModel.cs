using Newtonsoft.Json;

namespace Umat.Alumni.Member.Api.Models;

public class PaystackCallbackModel
{
    [JsonProperty("event")]
    public string? Event { get; set; }

    [JsonProperty("data")]
    public PaystackTransactionData? Data { get; set; }
}

public class PaystackTransactionData
{
    [JsonProperty("reference")]
    public string? Reference { get; set; }

    [JsonProperty("status")]
    public string? TransactionStatus { get; set; }

    [JsonProperty("amount")]
    public decimal? Amount { get; set; }
}
