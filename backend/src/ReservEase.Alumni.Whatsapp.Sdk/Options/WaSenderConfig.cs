namespace ReservEase.Alumni.Whatsapp.Sdk.Options;

public class WaSenderConfig
{
    public string BaseUrl { get; set; } = "https://www.wasenderapi.com";
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The shared secret WaSender sends in X-Webhook-Signature. Set the same value in the WaSender dashboard.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>The AlumUnion Assistant's own number, digits with country code. Used to build wa.me links; never to send.</summary>
    public string AssistantNumber { get; set; } = string.Empty;

    /// <summary>Master switch for the assistant and WhatsApp notifications. Off means AlumUnion behaves exactly as before.</summary>
    public bool Enabled { get; set; }
}
