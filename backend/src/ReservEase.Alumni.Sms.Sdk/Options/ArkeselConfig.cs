namespace ReservEase.Alumni.Sms.Sdk.Options;

public class ArkeselConfig
{
    public string BaseUrl { get; set; } = "https://sms.arkesel.com";
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The sender name recipients see (max 11 chars per Arkesel's requirements) — e.g. the institution's short name.</summary>
    public string SenderId { get; set; } = string.Empty;
}
