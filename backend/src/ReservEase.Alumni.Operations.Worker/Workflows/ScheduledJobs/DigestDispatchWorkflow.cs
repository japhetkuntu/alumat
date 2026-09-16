using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule (see ScheduledJobsRegistration) — the direct replacement
/// for Member.Api's in-process DigestSchedulerService BackgroundService. One run per
/// scheduled fire: resolve every active institution, send that institution's due digests,
/// isolate failures per institution so one bad tenant doesn't stop the rest.
/// </summary>
[Workflow("DigestDispatch")]
public class DigestDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveActiveInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        Workflow.Logger.LogInformation("Digest dispatch starting for {Count} institutions", institutionIds.Count);

        var totalSent = 0;
        foreach (var institutionId in institutionIds)
        {
            try
            {
                totalSent += await Workflow.ExecuteActivityAsync(
                    (ScheduledJobsActivities a) => a.SendDueDigestsForInstitutionAsync(institutionId),
                    ScheduledJobsActivityOptions.DatabaseWrite);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Digest dispatch failed for institution {InstitutionId}", institutionId);
            }
        }

        Workflow.Logger.LogInformation("Digest dispatch complete: {Sent} digests sent across {Count} institutions", totalSent, institutionIds.Count);
    }
}
