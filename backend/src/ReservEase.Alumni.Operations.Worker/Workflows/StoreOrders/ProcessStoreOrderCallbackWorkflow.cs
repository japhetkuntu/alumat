using Microsoft.Extensions.Logging;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using Temporalio.Common;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.StoreOrders;

[Workflow("ProcessStoreOrderCallback")]
public class ProcessStoreOrderCallbackWorkflow : IProcessStoreOrderCallbackWorkflow
{
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
            "[StoreOrderCallback] Processing {Provider} callback for reference {Reference}",
            request.Provider, request.Reference);

        await Workflow.ExecuteActivityAsync(
            (StoreOrderCallbackActivities a) => a.ProcessAsync(request.Reference, request.RawBody),
            ProcessOptions);
    }
}
