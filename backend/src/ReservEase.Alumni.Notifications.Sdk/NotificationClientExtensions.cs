using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.Temporal.Sdk;

namespace ReservEase.Alumni.Notifications.Sdk;

/// <summary>
/// The one call every project in the solution uses instead of the old
/// notificationActor.Tell(command)/INotificationDispatcher.DispatchXAsync(...) — swap the
/// injected dependency for ITemporalClientProvider and call this. Never throws and never
/// blocks its caller on Temporal being reachable, matching the actor's original contract:
/// if Temporal is down, the notification is dropped and logged rather than failing
/// whatever business operation triggered it (same trade-off the payment-callback webhook
/// path already makes via ITemporalClientProvider.IsAvailable).
/// </summary>
public static class NotificationClientExtensions
{
    public static async Task EnqueueNotificationAsync(
        this ITemporalClientProvider provider, NotificationRequest request, ILogger logger)
    {
        if (!provider.IsAvailable)
        {
            logger.LogWarning("Temporal unavailable — dropped notification {Kind} for institution {InstitutionId}", request.Kind, request.InstitutionId);
            return;
        }

        try
        {
            await provider.Client!.SignalWithStartAsync<INotificationDispatchWorkflow>(
                wf => wf.RunAsync(null),
                wf => wf.EnqueueAsync(request),
                NotificationWorkflowId.Value,
                NotificationTaskQueues.Dispatch);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue notification {Kind} for institution {InstitutionId}", request.Kind, request.InstitutionId);
        }
    }
}
