using Microsoft.Extensions.Logging;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker.Workflows.StoreOrders;

/// <summary>
/// Registered via AddScopedActivities, so each invocation gets its own DI scope
/// automatically — no manual IServiceScopeFactory handling needed, unlike the actor
/// this workflow replaces.
/// </summary>
public class StoreOrderCallbackActivities(
    IStoreOrderService storeOrderService,
    ILogger<StoreOrderCallbackActivities> logger)
{
    [Activity("StoreOrderCallback.Process")]
    public virtual async Task ProcessAsync(string reference, string rawBody)
    {
        try
        {
            await storeOrderService.ProcessPaystackCallbackAsync(reference, rawBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing store order callback for reference {Reference}", reference);
            throw new ApplicationFailureException($"Failed to process store order callback for reference {reference}", ex);
        }
    }
}
