using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule (see ScheduledJobsRegistration): reminds platform staff about tasks that are due soon or
/// overdue. Platform-wide, so unlike the per-institution jobs there is no institution loop.
/// </summary>
[Workflow("WorkReminderDispatch")]
public class WorkReminderDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var sent = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.SendDueWorkRemindersAsync(),
            ScheduledJobsActivityOptions.DatabaseWrite);
        Workflow.Logger.LogInformation("Work reminder dispatch complete: {Sent} reminders sent", sent);
    }
}
