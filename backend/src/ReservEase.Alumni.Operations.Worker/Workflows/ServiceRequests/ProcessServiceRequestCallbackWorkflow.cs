using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ServiceRequests;

[Workflow("ProcessServiceRequestCallback")]
public class ProcessServiceRequestCallbackWorkflow : IProcessServiceRequestCallbackWorkflow
{
    [WorkflowRun]
    public async Task<PaymentCallbackResult> RunAsync(ProcessPaymentCallbackRequest request)
    {
        var reference = request.Reference;
        Workflow.Logger.LogInformation(
            "[ServiceRequestCallback] Processing {Provider} callback for reference {Reference}",
            request.Provider, reference);

        var serviceRequest = await Workflow.ExecuteActivityAsync(
            (ServiceRequestCallbackActivities a) => a.LoadRequestAsync(reference),
            PaymentActivityOptions.DatabaseRead);

        if (serviceRequest is null)
        {
            // We only ever call Paystack's InitializePaymentAsync after our own
            // ServiceRequest row is durably committed, so a callback for a
            // reference with no matching row can't be our own data loss — it's a
            // wrong or bogus reference. Reject rather than accepting it silently.
            Workflow.Logger.LogError("No ServiceRequest found for reference {Reference}. Rejecting callback as an unknown reference.", reference);
            return PaymentCallbackResult.BadRequest("Unknown payment reference.");
        }

        if (!string.IsNullOrEmpty(request.RawBody))
            serviceRequest.CallbackPayload = request.RawBody;

        if (serviceRequest.PaymentStatus == "Successful")
        {
            if (!string.IsNullOrEmpty(request.RawBody))
                await Workflow.ExecuteActivityAsync((ServiceRequestCallbackActivities a) => a.SaveRequestAsync(serviceRequest), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.Ok("Payment already verified and recorded");
        }

        var verify = await Workflow.ExecuteActivityAsync(
            (ServiceRequestCallbackActivities a) => a.VerifyPaystackPaymentAsync(reference),
            PaymentActivityOptions.ExternalGateway);

        if (!verify.Status)
        {
            serviceRequest.PaymentStatus = "Failed";
            serviceRequest.FailureMessage = verify.Message;
            await Workflow.ExecuteActivityAsync((ServiceRequestCallbackActivities a) => a.SaveRequestAsync(serviceRequest), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.BadRequest(verify.Message ?? "Payment verification failed");
        }

        serviceRequest.GrossChargeAmount = verify.GrossAmount;
        if (verify.GatewayFee.HasValue)
            serviceRequest.GatewayFeeAmount = verify.GatewayFee.Value;
        serviceRequest.GatewayResponse = verify.GatewayResponse;

        if (!string.IsNullOrEmpty(request.RawBody))
        {
            try
            {
                var payload = JObject.Parse(request.RawBody);
                serviceRequest.Channel ??= payload.SelectToken("data.authorization.channel")?.ToString();
            }
            catch
            {
                // best effort; ignore if parsing fails
            }
        }

        if (verify.PaystackStatus == "success")
        {
            serviceRequest.PaymentStatus = "Successful";
            serviceRequest.ConfirmedAt = Workflow.UtcNow;

            var serviceType = await Workflow.ExecuteActivityAsync(
                (ServiceRequestCallbackActivities a) => a.LoadServiceTypeAsync(serviceRequest.ServiceTypeId),
                PaymentActivityOptions.DatabaseRead);

            serviceRequest.CurrentStage = serviceType?.Stages.FirstOrDefault() ?? "Submitted";
            serviceRequest.Updates.Add(new ServiceRequestUpdate { ChangedAt = Workflow.UtcNow, Stage = serviceRequest.CurrentStage });

            await Workflow.ExecuteActivityAsync((ServiceRequestCallbackActivities a) => a.SaveRequestAsync(serviceRequest), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.Ok("Payment verified and service request confirmed");
        }

        serviceRequest.PaymentStatus = "Failed";
        serviceRequest.FailureMessage = $"Payment {verify.PaystackStatus}.";
        await Workflow.ExecuteActivityAsync((ServiceRequestCallbackActivities a) => a.SaveRequestAsync(serviceRequest), PaymentActivityOptions.DatabaseWrite);
        return PaymentCallbackResult.Ok("Payment status updated");
    }
}
