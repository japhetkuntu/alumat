using Temporalio.Workflows;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Durable processing of one payment-gateway callback for a contribution (CN_ prefix).
/// Implemented by Operations.Worker; Member.Api's webhook controller starts this by
/// interface once it knows the reference belongs to a contribution.
/// </summary>
[Workflow("ProcessContributionCallback")]
public interface IProcessContributionCallbackWorkflow
{
    [WorkflowRun]
    Task<PaymentCallbackResult> RunAsync(ProcessPaymentCallbackRequest request);
}
