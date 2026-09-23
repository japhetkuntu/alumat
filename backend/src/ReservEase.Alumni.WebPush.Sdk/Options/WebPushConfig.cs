namespace ReservEase.Alumni.WebPush.Sdk.Options;

public class WebPushConfig
{
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>Required by the VAPID spec — a mailto: or https: URL identifying the sender, shown to push services (not to end users).</summary>
    public string VapidSubject { get; set; } = "mailto:ops@yourplatform.example";
}
