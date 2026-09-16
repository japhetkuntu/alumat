using Microsoft.Extensions.Logging;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using Temporalio.Common;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Contributions;

[Workflow("ProcessContributionCallback")]
public class ProcessContributionCallbackWorkflow : IProcessContributionCallbackWorkflow
{
    /// <summary>
    /// Financial-critical retry shape: keep retrying for up to 2 hours (a downstream outage
    /// shouldn't lose a payment callback) with exponential backoff so a struggling DB or
    /// gateway isn't hammered.
    /// </summary>
    private static readonly ActivityOptions ProcessOptions = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromHours(2),
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 0,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(5),
            MaximumInterval = TimeSpan.FromMinutes(5),
        },
    };

    [WorkflowRun]
    public async Task RunAsync(ProcessPaymentCallbackRequest request)
    {
        Workflow.Logger.LogInformation(
            "[ContributionCallback] Processing {Provider} callback for reference {Reference}",
            request.Provider, request.Reference);

        await Workflow.ExecuteActivityAsync(
            (ContributionCallbackActivities a) => a.ProcessAsync(request.Reference, request.RawBody),
            ProcessOptions);
    }
}
