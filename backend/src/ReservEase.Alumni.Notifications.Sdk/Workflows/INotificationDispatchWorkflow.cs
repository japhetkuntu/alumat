using ReservEase.Alumni.Notifications.Sdk.Models;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Notifications.Sdk.Workflows;

/// <summary>
/// Durable processing of exactly one notification. Implemented by Operations.Worker.
/// One short-lived workflow execution per request — started with a fresh, unique ID
/// each time (see NotificationClientExtensions.EnqueueNotificationAsync) — rather than
/// one perpetual global queue: every other Temporal workflow in this codebase
/// (ProcessContributionCallbackWorkflow and friends) already follows this
/// short-lived/request-scoped shape, and it sidesteps the class of TMPRL1100
/// nondeterminism error a long-running workflow accumulates every time its own code
/// changes underneath an execution that's still open. Callers never start-and-wait:
/// they fire this and move on immediately, same fire-and-forget contract the retired
/// Akka actor (and the old perpetual-queue version of this workflow) had. The
/// trade-off, made deliberately: no more strict global ordering across every
/// notification platform-wide — nothing in this domain relies on that, since each
/// notification is independent (different recipients, different kinds).
/// </summary>
[Workflow("NotificationDispatch")]
public interface INotificationDispatchWorkflow
{
    [WorkflowRun]
    Task RunAsync(NotificationRequest request);
}
