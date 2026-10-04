using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Reports.Sdk.Workflows;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Reports;

/// <summary>
/// See <see cref="IGenerateReportWorkflow"/> for why a report is a workflow at all. The sequence:
/// mark running → build and store the file → mark ready → tell the requester. If the file can't be
/// produced after its retries, the job is marked failed with a reason and the requester is told
/// that instead — a report never just sits at "preparing".
/// </summary>
[Workflow("GenerateReport")]
public class GenerateReportWorkflow : IGenerateReportWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string reportJobId)
    {
        var exists = await Workflow.ExecuteActivityAsync(
            (ReportActivities a) => a.MarkRunningAsync(reportJobId), ReportActivityOptions.Database);
        if (!exists)
        {
            Workflow.Logger.LogWarning("[GenerateReport] Report job {ReportJobId} not found; nothing to generate", reportJobId);
            return;
        }

        try
        {
            var file = await Workflow.ExecuteActivityAsync(
                (ReportActivities a) => a.GenerateAsync(reportJobId), ReportActivityOptions.Generation);
            await Workflow.ExecuteActivityAsync(
                (ReportActivities a) => a.MarkReadyAsync(reportJobId, file), ReportActivityOptions.Database);
        }
        catch (ActivityFailureException e)
        {
            Workflow.Logger.LogError(e, "[GenerateReport] Report job {ReportJobId} could not be generated", reportJobId);
            await Workflow.ExecuteActivityAsync(
                (ReportActivities a) => a.MarkFailedAsync(reportJobId, "We couldn't prepare this report. Please request it again."),
                ReportActivityOptions.Database);
        }

        // The report's outcome is already recorded; a notification that can't be sent must not undo it.
        try
        {
            await Workflow.ExecuteActivityAsync(
                (ReportActivities a) => a.NotifyRequesterAsync(reportJobId), ReportActivityOptions.Notify);
        }
        catch (ActivityFailureException e)
        {
            Workflow.Logger.LogWarning(e, "[GenerateReport] Could not notify the requester of report job {ReportJobId}", reportJobId);
        }
    }
}

/// <summary>Daily: deletes report files past their retention and fails any job that has been stuck unfinished. Started by a Temporal Schedule (see ScheduledJobsRegistration).</summary>
[Workflow("ReportCleanup")]
public class ReportCleanupWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var expired = await Workflow.ExecuteActivityAsync((ReportActivities a) => a.ExpireOldReportsAsync(), ReportActivityOptions.Generation);
        Workflow.Logger.LogInformation("[ReportCleanup] Cleaned up {Count} report jobs", expired);
    }
}

public static class ReportActivityOptions
{
    public static readonly ActivityOptions Database = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(10),
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy { BackoffCoefficient = 2.0f, InitialInterval = TimeSpan.FromSeconds(2), MaximumInterval = TimeSpan.FromMinutes(1) },
    };

    /// <summary>
    /// The long one. Up to half an hour per attempt, three attempts: a transient database or storage
    /// failure gets another go from the start, but a report that fails three times is broken, not
    /// unlucky. The heartbeat timeout is what notices a worker that died mid-report — without it the
    /// retry would wait out the full half hour first.
    /// </summary>
    public static readonly ActivityOptions Generation = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(30),
        HeartbeatTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 3,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(10),
            MaximumInterval = TimeSpan.FromMinutes(2),
            NonRetryableErrorTypes = [nameof(ReportNotSupportedException)],
        },
    };

    public static readonly ActivityOptions Notify = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 3, BackoffCoefficient = 2.0f, InitialInterval = TimeSpan.FromSeconds(5) },
    };
}
