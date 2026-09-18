using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker;

/// <summary>
/// Ensures the three periodic-job Temporal Schedules exist — the direct replacement for
/// Member.Api's in-process PeriodicTimer-based BackgroundServices. Runs once at startup;
/// idempotent across every restart/redeploy since CreateScheduleAsync throws
/// ScheduleAlreadyRunningException (caught and logged, not treated as an error) once a
/// schedule has been created the first time. Interval-based (not calendar/cron) since
/// these are plain "every N hours" jobs, matching the old schedulers' own tick constants.
/// </summary>
public static class ScheduledJobsRegistration
{
    public static async Task EnsureSchedulesAsync(IServiceProvider services)
    {
        var client = services.GetRequiredService<ITemporalClient>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        await EnsureScheduleAsync(client, logger, "digest-dispatch-schedule",
            ScheduleActionStartWorkflow.Create<DigestDispatchWorkflow>(
                wf => wf.RunAsync(),
                new WorkflowOptions { Id = "digest-dispatch", TaskQueue = OperationsTaskQueues.ScheduledJobs }),
            TimeSpan.FromHours(6));

        await EnsureScheduleAsync(client, logger, "birthday-spotlight-schedule",
            ScheduleActionStartWorkflow.Create<BirthdaySpotlightDispatchWorkflow>(
                wf => wf.RunAsync(),
                new WorkflowOptions { Id = "birthday-spotlight-dispatch", TaskQueue = OperationsTaskQueues.ScheduledJobs }),
            TimeSpan.FromHours(6));

        await EnsureScheduleAsync(client, logger, "recurring-giving-schedule",
            ScheduleActionStartWorkflow.Create<RecurringGivingWorkflow>(
                wf => wf.RunAsync(),
                new WorkflowOptions { Id = "recurring-giving-dispatch", TaskQueue = OperationsTaskQueues.ScheduledJobs }),
            TimeSpan.FromHours(24));

        await EnsureScheduleAsync(client, logger, "membership-reminder-schedule",
            ScheduleActionStartWorkflow.Create<MembershipReminderDispatchWorkflow>(
                wf => wf.RunAsync(),
                new WorkflowOptions { Id = "membership-reminder-dispatch", TaskQueue = OperationsTaskQueues.ScheduledJobs }),
            TimeSpan.FromHours(24));
    }

    private static async Task EnsureScheduleAsync(ITemporalClient client, ILogger logger, string scheduleId, ScheduleAction action, TimeSpan every)
    {
        try
        {
            var spec = new ScheduleSpec { Intervals = [new ScheduleIntervalSpec(every)] };
            await client.CreateScheduleAsync(scheduleId, new Schedule(action, spec), new ScheduleOptions());
            logger.LogInformation("Created schedule {ScheduleId} (every {Interval})", scheduleId, every);
        }
        catch (ScheduleAlreadyRunningException)
        {
            logger.LogInformation("Schedule {ScheduleId} already exists — leaving it as-is", scheduleId);
        }
    }
}
