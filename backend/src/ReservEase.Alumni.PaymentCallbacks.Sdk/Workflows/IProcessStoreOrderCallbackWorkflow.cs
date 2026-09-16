using Temporalio.Workflows;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Durable processing of one payment-gateway callback for a store order (SO_ prefix).
/// Implemented by Operations.Worker; Member.Api's webhook controller starts this by
/// interface once it knows the reference belongs to a store order.
/// </summary>
[Workflow("ProcessStoreOrderCallback")]
public interface IProcessStoreOrderCallbackWorkflow
{
    [WorkflowRun]
    Task RunAsync(ProcessPaymentCallbackRequest request);
}
