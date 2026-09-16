namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Temporal task queues served by Operations.Worker. Payment-callback processing is its
/// first job, not its only one — future Temporal-backed operations get their own constant
/// here rather than a new worker project.
/// </summary>
public static class OperationsTaskQueues
{
    public const string PaymentCallbackProcessing = "operations-payment-callback-processing";
}
