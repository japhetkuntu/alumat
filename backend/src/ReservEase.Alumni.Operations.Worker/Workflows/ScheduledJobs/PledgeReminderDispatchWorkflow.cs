using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule (see ScheduledJobsRegistration). One run per fire: resolve every active
/// institution, send that institution's due pledge reminders, isolate failures per institution.
/// </summary>
[Workflow("PledgeReminderDispatch")]
public class PledgeReminderDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveActiveInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        var totalSent = 0;
        foreach (var institutionId in institutionIds)
        {
            try
            {
                totalSent += await Workflow.ExecuteActivityAsync(
                    (ScheduledJobsActivities a) => a.SendDuePledgeRemindersForInstitutionAsync(institutionId),
                    ScheduledJobsActivityOptions.DatabaseWrite);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Pledge reminder dispatch failed for institution {InstitutionId}", institutionId);
            }
        }

        Workflow.Logger.LogInformation("Pledge reminder dispatch complete: {Sent} reminders emailed across {Count} institutions", totalSent, institutionIds.Count);
    }
}
