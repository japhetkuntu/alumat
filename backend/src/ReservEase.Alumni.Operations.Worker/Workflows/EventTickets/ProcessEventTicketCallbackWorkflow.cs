using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.EventTickets;

[Workflow("ProcessEventTicketCallback")]
public class ProcessEventTicketCallbackWorkflow : IProcessEventTicketCallbackWorkflow
{
    [WorkflowRun]
    public async Task<PaymentCallbackResult> RunAsync(ProcessPaymentCallbackRequest request)
    {
        var reference = request.Reference;
        Workflow.Logger.LogInformation("[EventTicketCallback] Processing {Provider} callback for reference {Reference}", request.Provider, reference);

        var ticket = await Workflow.ExecuteActivityAsync(
            (EventTicketCallbackActivities a) => a.LoadTicketAsync(reference), PaymentActivityOptions.DatabaseRead);

        if (ticket is null)
        {
            // The pending row is saved before Paystack is ever told about the reference, so a reference with no row is
            // a wrong or bogus one (or an earlier attempt the member has since replaced). Reject it.
            Workflow.Logger.LogError("No event ticket found for reference {Reference}. Rejecting callback as an unknown reference.", reference);
            return PaymentCallbackResult.BadRequest("Unknown payment reference.");
        }

        if (!string.IsNullOrEmpty(request.RawBody))
            ticket.CallbackPayload = request.RawBody;

        if (ticket.Status == EventRsvpStatuses.Confirmed)
        {
            if (!string.IsNullOrEmpty(request.RawBody))
                await Workflow.ExecuteActivityAsync((EventTicketCallbackActivities a) => a.SaveTicketAsync(ticket), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.Ok("Payment already verified and recorded");
        }

        var verify = await Workflow.ExecuteActivityAsync(
            (EventTicketCallbackActivities a) => a.VerifyPaystackPaymentAsync(reference), PaymentActivityOptions.ExternalGateway);

        if (!verify.Status)
        {
            ticket.Status = EventRsvpStatuses.PaymentFailed;
            ticket.FailureMessage = verify.Message;
            await Workflow.ExecuteActivityAsync((EventTicketCallbackActivities a) => a.SaveTicketAsync(ticket), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.BadRequest(verify.Message ?? "Payment verification failed");
        }

        ticket.GrossChargeAmount = verify.GrossAmount;
        if (verify.GatewayFee.HasValue)
            ticket.GatewayFeeAmount = verify.GatewayFee.Value;
        ticket.GatewayResponse = verify.GatewayResponse;

        if (!string.IsNullOrEmpty(request.RawBody))
        {
            try
            {
                ticket.Channel ??= JObject.Parse(request.RawBody).SelectToken("data.authorization.channel")?.ToString();
            }
            catch
            {
                // best effort; ignore if parsing fails
            }
        }

        if (verify.PaystackStatus != "success")
        {
            ticket.Status = EventRsvpStatuses.PaymentFailed;
            ticket.FailureMessage = $"Payment {verify.PaystackStatus}.";
            await Workflow.ExecuteActivityAsync((EventTicketCallbackActivities a) => a.SaveTicketAsync(ticket), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.Ok("Payment status updated");
        }

        ticket.Status = EventRsvpStatuses.Confirmed;
        ticket.PaidAt = Workflow.UtcNow;
        ticket.FailureMessage = null;
        await Workflow.ExecuteActivityAsync((EventTicketCallbackActivities a) => a.SaveTicketAsync(ticket), PaymentActivityOptions.DatabaseWrite);

        var seats = await Workflow.ExecuteActivityAsync(
            (EventTicketCallbackActivities a) => a.SyncSeatCountAsync(ticket.EventId), PaymentActivityOptions.DatabaseWrite);
        if (seats.Capacity.HasValue && seats.Count > seats.Capacity.Value)
            Workflow.Logger.LogWarning("Event {EventId} is over capacity ({Count}/{Capacity}) after ticket {Reference} was paid", ticket.EventId, seats.Count, seats.Capacity, reference);

        var title = seats.EventTitle ?? "your event";
        await Workflow.ExecuteActivityAsync(
            (EventTicketCallbackActivities a) => a.DispatchTicketConfirmedAsync(ticket.InstitutionId, ticket.MemberId, ticket.EventId, title),
            PaymentActivityOptions.Notification);

        return PaymentCallbackResult.Ok("Payment verified and ticket confirmed");
    }
}
