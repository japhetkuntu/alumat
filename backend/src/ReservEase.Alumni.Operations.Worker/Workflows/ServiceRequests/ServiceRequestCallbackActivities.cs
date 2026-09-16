using Microsoft.Extensions.Logging;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ServiceRequests;

/// <summary>
/// Registered via AddScopedActivities, so each invocation gets its own DI scope
/// automatically — no manual IServiceScopeFactory handling needed, unlike the actor
/// this workflow replaces.
/// </summary>
public class ServiceRequestCallbackActivities(
    IServiceRequestService serviceRequestService,
    ILogger<ServiceRequestCallbackActivities> logger)
{
    [Activity("ServiceRequestCallback.Process")]
    public virtual async Task ProcessAsync(string reference, string rawBody)
    {
        try
        {
            await serviceRequestService.ProcessPaystackCallbackAsync(reference, rawBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing service request callback for reference {Reference}", reference);
            throw new ApplicationFailureException($"Failed to process service request callback for reference {reference}", ex);
        }
    }
}
