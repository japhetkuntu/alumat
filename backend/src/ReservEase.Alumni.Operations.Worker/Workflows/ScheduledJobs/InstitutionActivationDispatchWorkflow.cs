using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired daily on a Temporal Schedule (see ScheduledJobsRegistration). Per run:
/// upsert this week's activity snapshot for every active institution (the
/// history "staff active three weeks in a row" needs), evaluate activation and
/// stamp newly activated institutions, nudge admins of institutions still short
/// of it, remind lead owners of due follow-ups, and on Mondays send platform
/// staff the weekly digest. Failures are
/// isolated per institution, mirroring MembershipReminderDispatchWorkflow.
/// </summary>
[Workflow("InstitutionActivationDispatch")]
public class InstitutionActivationDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveActiveInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        foreach (var institutionId in institutionIds)
        {
            try
            {
                await Workflow.ExecuteActivityAsync(
                    (InstitutionActivationActivities a) => a.RecordActivitySnapshotAsync(institutionId),
                    ScheduledJobsActivityOptions.DatabaseWrite);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Activity snapshot failed for institution {InstitutionId}", institutionId);
            }
        }

        var items = await Workflow.ExecuteActivityAsync(
            (InstitutionActivationActivities a) => a.EvaluateAllAsync(),
            ScheduledJobsActivityOptions.DatabaseWrite);

        var nudged = 0;
        foreach (var item in items.Where(i => !i.IsActivated))
        {
            try
            {
                if (await Workflow.ExecuteActivityAsync(
                        (InstitutionActivationActivities a) => a.SendNudgeAsync(item),
                        ScheduledJobsActivityOptions.DatabaseWrite))
                    nudged++;
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Activation nudge failed for institution {InstitutionId}", item.InstitutionId);
            }
        }

        var followUps = 0;
        try
        {
            followUps = await Workflow.ExecuteActivityAsync(
                (InstitutionActivationActivities a) => a.SendFollowUpRemindersAsync(),
                ScheduledJobsActivityOptions.DatabaseWrite);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogError(ex, "Lead follow-up reminders failed");
        }

        if (Workflow.UtcNow.DayOfWeek == DayOfWeek.Monday)
        {
            await Workflow.ExecuteActivityAsync(
                (InstitutionActivationActivities a) => a.SendPlatformDigestAsync(items),
                ScheduledJobsActivityOptions.DatabaseWrite);
        }

        Workflow.Logger.LogInformation("Activation dispatch complete: {Activated}/{Total} activated, {Nudged} nudged, {FollowUps} lead follow-ups",
            items.Count(i => i.IsActivated), items.Count, nudged, followUps);
    }
}
