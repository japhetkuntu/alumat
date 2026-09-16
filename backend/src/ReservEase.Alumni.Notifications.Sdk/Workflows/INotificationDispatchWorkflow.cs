using ReservEase.Alumni.Notifications.Sdk.Models;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Notifications.Sdk.Workflows;

/// <summary>
/// One long-lived, perpetual workflow instance (a single global singleton — see
/// NotificationWorkflowId) that every notification in the platform routes through.
/// Implemented by Operations.Worker. Callers never start-and-wait on this: they
/// signal-with-start it (see WorkflowClientExtensions.SignalWithStartAsync /
/// NotificationClientExtensions.EnqueueNotificationAsync) and move on immediately,
/// same fire-and-forget contract the Akka actor it replaces had.
/// </summary>
[Workflow("NotificationDispatch")]
public interface INotificationDispatchWorkflow
{
    [WorkflowRun]
    Task RunAsync(List<NotificationRequest>? carryOver);

    /// <summary>Enqueues one request for processing. Never throws, never awaits any
    /// I/O itself — the workflow's own loop (and its activities) does all the work.</summary>
    [WorkflowSignal("Enqueue")]
    Task EnqueueAsync(NotificationRequest request);
}
