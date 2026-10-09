using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired every few hours on a Temporal Schedule. For each active institution, in turn: refresh the health reading and
/// recommendations (idempotent, so the frequency is harmless), then, only inside the daytime sending window, remind
/// administrators who have been away and write to quiet members. A failure for one institution never stops the others.
/// </summary>
[Workflow("EngagementAutomation")]
public class EngagementAutomationWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var ids = await Workflow.ExecuteActivityAsync((EngagementActivities a) => a.ResolveInstitutionIdsAsync(), ScheduledJobsActivityOptions.DatabaseRead);
        int refreshed = 0, admins = 0, members = 0, failed = 0;
        foreach (var id in ids)
        {
            try
            {
                await Workflow.ExecuteActivityAsync((EngagementActivities a) => a.RefreshInstitutionAsync(id), ScheduledJobsActivityOptions.DatabaseWrite);
                refreshed++;
                admins += await Workflow.ExecuteActivityAsync((EngagementActivities a) => a.SendAdminMessagesAsync(id), ScheduledJobsActivityOptions.DatabaseWrite);
                members += await Workflow.ExecuteActivityAsync((EngagementActivities a) => a.SendMemberReengagementAsync(id), ScheduledJobsActivityOptions.DatabaseWrite);
            }
            catch (Exception ex)
            {
                failed++;
                Workflow.Logger.LogError(ex, "Engagement automation failed for institution {InstitutionId}", id);
            }
        }
        Workflow.Logger.LogInformation("Engagement automation: {Refreshed} refreshed, {Admins} administrator messages, {Members} member messages, {Failed} failed of {Count}", refreshed, admins, members, failed, ids.Count);
    }
}
