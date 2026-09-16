namespace ReservEase.Alumni.Notifications.Sdk.Workflows;

/// <summary>A single fixed workflow ID — NotificationDispatchWorkflow is one perpetual
/// global instance for the whole platform (not per-tenant), since InstitutionId already
/// travels inside each NotificationRequest. Every signal-with-start call across every
/// project targets this same ID; the workflow keeps itself alive forever via
/// continue-as-new.</summary>
public static class NotificationWorkflowId
{
    public const string Value = "notification-dispatcher";
}
