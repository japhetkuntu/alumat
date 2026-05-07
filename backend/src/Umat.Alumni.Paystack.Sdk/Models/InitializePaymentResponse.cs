

using Newtonsoft.Json;

namespace Umat.Alumni.Paystack.Sdk.Models;

public class InitializePaymentResponse
{
    [JsonProperty("status")]
    public bool Status { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("data")]
    public InitializePaymentData? Data { get; set; }
}

public class InitializePaymentData
{
    [JsonProperty("authorization_url")]
    public string? AuthorizationUrl { get; set; }

    [JsonProperty("access_code")]
    public string? AccessCode { get; set; }

    [JsonProperty("reference")]
    public string? Reference { get; set; }
}
