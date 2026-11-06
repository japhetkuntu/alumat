using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule. A request to connect that has waited three days with no answer gets one reminder, so a member who registered
/// once and stopped visiting still hears that someone is waiting. See ScheduledJobsActivities.SendIntroductionRemindersAsync for the rules.
/// </summary>
[Workflow("IntroductionReminderDispatch")]
public class IntroductionReminderDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        try
        {
            var reminded = await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.SendIntroductionRemindersAsync(Workflow.UtcNow),
                ScheduledJobsActivityOptions.DatabaseWrite);
            Workflow.Logger.LogInformation("Introduction reminders sent: {Count}", reminded);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogError(ex, "Introduction reminder dispatch failed");
        }
    }
}
