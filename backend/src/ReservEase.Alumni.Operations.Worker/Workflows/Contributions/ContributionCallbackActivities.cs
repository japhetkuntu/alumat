using Microsoft.Extensions.Logging;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Contributions;

/// <summary>
/// Registered via AddScopedActivities, so each invocation gets its own DI scope
/// automatically — no manual IServiceScopeFactory handling needed, unlike the actor
/// this workflow replaces.
/// </summary>
public class ContributionCallbackActivities(
    IContributionService contributionService,
    ILogger<ContributionCallbackActivities> logger)
{
    [Activity("ContributionCallback.Process")]
    public virtual async Task ProcessAsync(string reference, string rawBody)
    {
        try
        {
            await contributionService.ProcessPaystackCallbackAsync(reference, rawBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing contribution callback for reference {Reference}", reference);
            throw new ApplicationFailureException($"Failed to process contribution callback for reference {reference}", ex);
        }
    }
}
