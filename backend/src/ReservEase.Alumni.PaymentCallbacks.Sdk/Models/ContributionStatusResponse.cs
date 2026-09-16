using Newtonsoft.Json;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Models;

public class ContributionStatusResponse
{
    [JsonProperty("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("amount")]
    public decimal? Amount { get; set; }

    [JsonProperty("paymentMethod")]
    public string? PaymentMethod { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }
}
