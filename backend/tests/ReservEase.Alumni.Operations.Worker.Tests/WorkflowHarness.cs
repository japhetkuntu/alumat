using System.Reflection;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Tests;

public static class WorkflowHarness
{
    /// <summary>Runs one payment-callback workflow to completion on a throwaway task queue, with the given activity instances registered.</summary>
    public static async Task<PaymentCallbackResult> RunCallback<TWorkflow>(ITemporalClient client, string reference, string? rawBody, params object[] activities)
    {
        var queue = "q-" + Guid.NewGuid().ToString("N");
        var options = new TemporalWorkerOptions(queue).AddWorkflow<TWorkflow>();
        foreach (var a in activities) options.AddAllActivities(a.GetType(), a);
        var name = typeof(TWorkflow).GetCustomAttribute<WorkflowAttribute>()!.Name!;
        using var worker = new TemporalWorker(client, options);
        return await worker.ExecuteAsync(() => client.ExecuteWorkflowAsync<PaymentCallbackResult>(
            name, new object?[] { new ProcessPaymentCallbackRequest("Paystack", reference, rawBody!) },
            new WorkflowOptions("wf-" + Guid.NewGuid().ToString("N"), queue)));
    }
}
