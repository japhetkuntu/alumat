namespace ReservEase.Alumni.Notifications.Sdk.Workflows;

/// <summary>Its own task queue, isolated from OperationsTaskQueues.PaymentCallbackProcessing
/// (in PaymentCallbacks.Sdk) — a burst of notification traffic (e.g. a platform-wide
/// broadcast) must never head-of-line-block payment-callback processing, and vice versa.
/// Operations.Worker hosts both queues in the same process via two AddHostedTemporalWorker
/// calls.</summary>
public static class NotificationTaskQueues
{
    public const string Dispatch = "operations-notification-dispatch";
}
