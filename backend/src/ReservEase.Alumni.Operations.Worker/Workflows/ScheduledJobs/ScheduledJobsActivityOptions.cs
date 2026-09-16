using Temporalio.Common;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>Same per-category retry-policy shape as PaymentActivityOptions/
/// NotificationActivityOptions — duplicated deliberately rather than shared, matching
/// this codebase's established convention of each workflow family owning its own tiers.</summary>
public static class ScheduledJobsActivityOptions
{
    /// <summary>Fast local read — a quick failure is fine to surface rather than
    /// retry for a long time.</summary>
    public static readonly ActivityOptions DatabaseRead = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(2),
        StartToCloseTimeout = TimeSpan.FromSeconds(15),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 0,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(2),
            MaximumInterval = TimeSpan.FromSeconds(30),
        },
    };

    /// <summary>A write (or a composite scan-and-write like a digest run) must not be
    /// lost — retry for longer than a read before giving up.</summary>
    public static readonly ActivityOptions DatabaseWrite = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(30),
        StartToCloseTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 0,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(5),
            MaximumInterval = TimeSpan.FromMinutes(2),
        },
    };

    /// <summary>Financial-critical: the Paystack recurring-charge call. Keep retrying for
    /// up to 2 hours (a transient gateway outage shouldn't cost a member their charge)
    /// with exponential backoff — same shape as PaymentActivityOptions.ExternalGateway.</summary>
    public static readonly ActivityOptions ExternalGateway = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromHours(2),
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 0,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(5),
            MaximumInterval = TimeSpan.FromMinutes(5),
        },
    };
}
