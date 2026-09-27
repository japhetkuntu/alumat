using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule (see ScheduledJobsRegistration) — sends a "tomorrow" reminder
/// (with a "see who else is going" count) to everyone with a confirmed RSVP on an event
/// starting in roughly 12-36 hours. One run per scheduled fire: resolve every active
/// institution, dispatch that institution's due reminders, isolate failures per institution.
/// </summary>
[Workflow("EventReminderDispatch")]
public class EventReminderDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveActiveInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        Workflow.Logger.LogInformation("Event reminder dispatch starting for {Count} institutions", institutionIds.Count);

        var totalSent = 0;
        foreach (var institutionId in institutionIds)
        {
            try
            {
                totalSent += await Workflow.ExecuteActivityAsync(
                    (ScheduledJobsActivities a) => a.SendDueEventRemindersForInstitutionAsync(institutionId),
                    ScheduledJobsActivityOptions.DatabaseWrite);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Event reminder dispatch failed for institution {InstitutionId}", institutionId);
            }
        }

        Workflow.Logger.LogInformation("Event reminder dispatch complete: {Sent} reminders sent across {Count} institutions", totalSent, institutionIds.Count);
    }
}
