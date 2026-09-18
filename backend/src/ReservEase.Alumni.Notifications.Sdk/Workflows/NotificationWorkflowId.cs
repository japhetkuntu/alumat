namespace ReservEase.Alumni.Notifications.Sdk.Workflows;

/// <summary>
/// NotificationDispatchWorkflow is one short-lived execution per notification (see its own
/// doc comment for why — this replaced an earlier design where every notification shared
/// one perpetual global instance under a single fixed ID). Each call gets a fresh, unique
/// ID here rather than a caller-supplied one: nothing about a notification needs to be
/// addressable/attachable later the way a payment callback's own reference is, so there's
/// no natural idempotency key to reuse instead.
/// </summary>
public static class NotificationWorkflowId
{
    public static string New() => $"notification-{Guid.NewGuid():N}";
}
