using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Operations.Worker.Models;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule — the direct replacement for Member.Api's in-process
/// BirthdaySpotlightSchedulerService. Only month/day of DateOfBirth ever matters; when
/// several members share a day they get ONE combined spotlight (see BuildCopy) rather
/// than N separate ones.
/// </summary>
[Workflow("BirthdaySpotlightDispatch")]
public class BirthdaySpotlightDispatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveBirthdaySpotlightEligibleInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        Workflow.Logger.LogInformation("Birthday spotlight dispatch starting for {Count} institutions", institutionIds.Count);

        var today = Workflow.UtcNow.Date;
        var createdCount = 0;

        foreach (var institutionId in institutionIds)
        {
            try
            {
                var celebrants = await Workflow.ExecuteActivityAsync(
                    (ScheduledJobsActivities a) => a.LoadTodaysCelebrantsAsync(institutionId, today),
                    ScheduledJobsActivityOptions.DatabaseRead);

                if (celebrants.Count == 0) continue;

                var (title, story) = BuildCopy(celebrants);
                await Workflow.ExecuteActivityAsync(
                    (ScheduledJobsActivities a) => a.CreateBirthdaySpotlightAsync(institutionId, celebrants, title, story, today),
                    ScheduledJobsActivityOptions.DatabaseWrite);
                createdCount++;

                Workflow.Logger.LogInformation("Created birthday spotlight for institution {InstitutionId} covering {Count} member(s)", institutionId, celebrants.Count);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Birthday spotlight dispatch failed for institution {InstitutionId}", institutionId);
            }
        }

        Workflow.Logger.LogInformation("Birthday spotlight dispatch complete: {Created} spotlight(s) created across {Count} institutions", createdCount, institutionIds.Count);
    }

    private static (string Title, string Story) BuildCopy(List<BirthdayCelebrant> celebrants)
    {
        var names = celebrants.Select(m => m.FirstName).ToList();
        if (celebrants.Count == 1)
        {
            var m = celebrants[0];
            return ($"Happy Birthday, {m.FirstName} {m.LastName}!",
                $"Join us in wishing {m.FirstName} {m.LastName} a very happy birthday today!");
        }

        if (celebrants.Count == 2)
        {
            return ($"Happy Birthday, {names[0]} & {names[1]}!",
                $"Two birthdays today! Join us in wishing {names[0]} {celebrants[0].LastName} and {names[1]} {celebrants[1].LastName} a very happy birthday.");
        }

        var fullNames = celebrants.Select(m => $"{m.FirstName} {m.LastName}").ToList();
        var listed = string.Join(", ", fullNames[..^1]) + $", and {fullNames[^1]}";
        return ($"Happy Birthday to {celebrants.Count} of our alumni today!",
            $"It's a big day for {celebrants.Count} members of our community: {listed}. Join us in wishing them all a very happy birthday!");
    }
}
