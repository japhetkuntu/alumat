namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Temporal task queues served by Operations.Worker. Payment-callback processing is its
/// first job, not its only one — future Temporal-backed operations get their own constant
/// here rather than a new worker project.
/// </summary>
public static class OperationsTaskQueues
{
    public const string PaymentCallbackProcessing = "operations-payment-callback-processing";

    /// <summary>Hosts the periodic Temporal-Schedule-driven jobs (digest emails, birthday
    /// spotlights, recurring giving charges) that replaced Member.Api's in-process
    /// BackgroundServices. One shared queue — real volume is a handful of fires per day,
    /// so there's no head-of-line risk between them.</summary>
    public const string ScheduledJobs = "operations-scheduled-jobs";
}
