using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ServiceRequests;

/// <summary>
/// One activity, one I/O call — the workflow (ProcessServiceRequestCallbackWorkflow)
/// owns the sequencing and every branch/decision. Registered via
/// AddScopedActivities, so each call gets its own DI scope automatically.
/// </summary>
public class ServiceRequestCallbackActivities(
    IAlumniPgRepository<ServiceRequest> requestRepo,
    IAlumniPgRepository<ServiceType> serviceTypeRepo,
    IPaystackService paystackService,
    ILogger<ServiceRequestCallbackActivities> logger)
{
    [Activity("ServiceRequestCallback.LoadRequest")]
    public virtual Task<ServiceRequest?> LoadRequestAsync(string reference) =>
        Wrap(() => requestRepo.GetOneAsync(r => r.TransactionRef == reference, ignoreQueryFilters: true), "load service request", reference);

    [Activity("ServiceRequestCallback.SaveRequest")]
    public virtual Task SaveRequestAsync(ServiceRequest request) =>
        Wrap(() => requestRepo.UpdateAsync(request), "save service request", request.Id);

    [Activity("ServiceRequestCallback.VerifyPaystackPayment")]
    public virtual Task<ServiceRequestPaystackVerifyResult> VerifyPaystackPaymentAsync(string reference) =>
        Wrap(async () =>
        {
            var response = await paystackService.VerifyPaymentAsync(reference);
            var amount = (response.Data?.Amount ?? 0) / 100m;
            var fee = response.Data?.Fees.HasValue == true ? response.Data!.Fees!.Value / 100m : (decimal?)null;
            return new ServiceRequestPaystackVerifyResult
            {
                Status = response.Status,
                Message = response.Message,
                PaystackStatus = response.Data?.Status?.ToLowerInvariant() ?? "unknown",
                GrossAmount = amount,
                GatewayFee = fee,
                GatewayResponse = response.Data?.GatewayResponse,
            };
        }, "verify Paystack payment", reference);

    [Activity("ServiceRequestCallback.LoadServiceType")]
    public virtual Task<ServiceType?> LoadServiceTypeAsync(string serviceTypeId) =>
        Wrap(() => serviceTypeRepo.GetOneAsync(s => s.Id == serviceTypeId, ignoreQueryFilters: true), "load service type", serviceTypeId);

    private static async Task<T> Wrap<T>(Func<Task<T>> action, string what, string context)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }

    private static async Task Wrap(Func<Task> action, string what, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }
}
