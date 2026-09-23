using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.WebPush.Sdk.Options;
using VapidDetails = WebPush.VapidDetails;
using WebPushClient = WebPush.WebPushClient;
using WebPushException = WebPush.WebPushException;

namespace ReservEase.Alumni.WebPush.Sdk.Services;

/// <summary>Sends Web Push notifications via VAPID, using the `WebPush` NuGet package for
/// JWT signing + aes128gcm payload encryption (no such crypto exists elsewhere in this
/// codebase, unlike the raw-HttpClient pattern used by Arkesel/WaSender).</summary>
public class WebPushService(
    IOptions<WebPushConfig> options,
    ILogger<WebPushService> logger) : IWebPushService
{
    private readonly WebPushConfig config = options.Value;

    public async Task<WebPushSendResult> SendAsync(PushSubscriptionDto subscription, string title, string body, string? actionUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new WebPushClient();
            var vapid = new VapidDetails(config.VapidSubject, config.VapidPublicKey, config.VapidPrivateKey);
            var pushSubscription = new global::WebPush.PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
            var payload = JsonSerializer.Serialize(new { title, body, actionUrl });

            await client.SendNotificationAsync(pushSubscription, payload, vapid, cancellationToken: cancellationToken);

            logger.LogInformation("Web push sent to {Endpoint}", subscription.Endpoint);
            return WebPushSendResult.Sent;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            logger.LogInformation("Push subscription gone ({Status}): {Endpoint}", ex.StatusCode, subscription.Endpoint);
            return WebPushSendResult.Gone;
        }
        catch (WebPushException ex)
        {
            logger.LogWarning(ex, "Web push send failed ({Status}) for {Endpoint}", ex.StatusCode, subscription.Endpoint);
            return WebPushSendResult.TransientFailure;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Web push send threw for {Endpoint}", subscription.Endpoint);
            return WebPushSendResult.TransientFailure;
        }
    }
}
