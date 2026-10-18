using Temporalio.Workflows;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Durable processing of one payment-gateway callback for an event ticket (ET_ prefix).
/// Implemented by Operations.Worker; Member.Api's webhook controller starts this by interface.
/// </summary>
[Workflow("ProcessEventTicketCallback")]
public interface IProcessEventTicketCallbackWorkflow
{
    [WorkflowRun]
    Task<PaymentCallbackResult> RunAsync(ProcessPaymentCallbackRequest request);
}
