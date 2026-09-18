using System.Linq.Expressions;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Temporal.Sdk;

public static class WorkflowClientExtensions
{
    /// <summary>
    /// Starts the workflow; if one is already running for this ID, attaches to that
    /// execution instead of treating it as an error. Used by both a fire-and-forget
    /// caller (the webhook controller — ignores the returned handle) and a
    /// wait-for-result caller (a "check my payment status" endpoint — awaits
    /// handle.GetResultAsync()). AllowDuplicate is deliberate: a workflow that
    /// already closed doesn't mean "already recorded" — the domain logic inside it
    /// can legitimately no-op and still complete successfully, so a later legitimate
    /// re-check (or Paystack's own webhook retry) must be able to start a fresh
    /// execution. AllowDuplicate still refuses a second execution while one is
    /// actively RUNNING, which is what this method's catch handles.
    /// </summary>
    public static async Task<WorkflowHandle<TWorkflow>> StartOrAttachAsync<TWorkflow>(
        this ITemporalClient client,
        Expression<Func<TWorkflow, Task>> runCall,
        string workflowId,
        string taskQueue)
    {
        try
        {
            return await client.StartWorkflowAsync(
                runCall,
                new WorkflowOptions
                {
                    Id = workflowId,
                    TaskQueue = taskQueue,
                    IdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate,
                });
        }
        catch (WorkflowAlreadyStartedException)
        {
            return client.GetWorkflowHandle<TWorkflow>(workflowId);
        }
    }

    /// <summary>Result-returning variant — used by "check my payment status" callers
    /// that need to await the workflow's outcome, not just fire it.</summary>
    public static async Task<WorkflowHandle<TWorkflow, TResult>> StartOrAttachAsync<TWorkflow, TResult>(
        this ITemporalClient client,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        string workflowId,
        string taskQueue)
    {
        try
        {
            return await client.StartWorkflowAsync(
                runCall,
                new WorkflowOptions
                {
                    Id = workflowId,
                    TaskQueue = taskQueue,
                    IdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate,
                });
        }
        catch (WorkflowAlreadyStartedException)
        {
            return client.GetWorkflowHandle<TWorkflow, TResult>(workflowId);
        }
    }
}
