using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule (see ScheduledJobsRegistration) — closes the "membership
/// dues reminders" gap: NotificationPreference.MembershipReminders existed but nothing ever
/// sent one. One run per scheduled fire: resolve every active institution, send that
/// institution's due reminders, isolate failures per institution so one bad tenant doesn't
/// stop the rest. Mirrors DigestDispatchWorkflow exactly.
/// </summary>
[Workflow("MembershipReminderDispatch")]
public class MembershipReminderDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveActiveInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        Workflow.Logger.LogInformation("Membership reminder dispatch starting for {Count} institutions", institutionIds.Count);

        var totalSent = 0;
        foreach (var institutionId in institutionIds)
        {
            try
            {
                totalSent += await Workflow.ExecuteActivityAsync(
                    (ScheduledJobsActivities a) => a.SendDueMembershipRemindersForInstitutionAsync(institutionId),
                    ScheduledJobsActivityOptions.DatabaseWrite);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Membership reminder dispatch failed for institution {InstitutionId}", institutionId);
            }
        }

        Workflow.Logger.LogInformation("Membership reminder dispatch complete: {Sent} reminders sent across {Count} institutions", totalSent, institutionIds.Count);
    }
}
