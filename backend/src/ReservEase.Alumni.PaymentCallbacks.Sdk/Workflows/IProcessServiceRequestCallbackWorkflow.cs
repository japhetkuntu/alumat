using Temporalio.Workflows;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Durable processing of one payment-gateway callback for a service request (SR_ prefix).
/// Implemented by Operations.Worker; Member.Api's webhook controller starts this by
/// interface once it knows the reference belongs to a service request.
/// </summary>
[Workflow("ProcessServiceRequestCallback")]
public interface IProcessServiceRequestCallbackWorkflow
{
    [WorkflowRun]
    Task<PaymentCallbackResult> RunAsync(ProcessPaymentCallbackRequest request);
}
