namespace ReservEase.Alumni.WebPush.Sdk.Services;

public record PushSubscriptionDto(string Endpoint, string P256dh, string Auth);

public enum WebPushSendResult
{
    Sent,
    TransientFailure,

    /// <summary>Push service reported 404/410 — the subscription is dead and should be deactivated.</summary>
    Gone,
}

public interface IWebPushService
{
    Task<WebPushSendResult> SendAsync(PushSubscriptionDto subscription, string title, string body, string? actionUrl, CancellationToken cancellationToken = default);
}
