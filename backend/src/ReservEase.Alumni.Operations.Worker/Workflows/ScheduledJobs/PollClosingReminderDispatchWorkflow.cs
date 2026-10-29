using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule (see ScheduledJobsRegistration): in the last day of a poll, reminds the eligible members who have
/// not voted. One run resolves every active institution and sends that institution's due reminders; a failure in one does not stop the rest.
/// </summary>
[Workflow("PollClosingReminderDispatch")]
public class PollClosingReminderDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveActiveInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        var total = 0;
        foreach (var institutionId in institutionIds)
        {
            try
            {
                total += await Workflow.ExecuteActivityAsync(
                    (ScheduledJobsActivities a) => a.SendPollClosingRemindersForInstitutionAsync(institutionId),
                    ScheduledJobsActivityOptions.DatabaseWrite);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Poll closing reminders failed for institution {InstitutionId}", institutionId);
            }
        }
        Workflow.Logger.LogInformation("Poll closing reminders complete: {Sent} reminders across {Count} institutions", total, institutionIds.Count);
    }
}
