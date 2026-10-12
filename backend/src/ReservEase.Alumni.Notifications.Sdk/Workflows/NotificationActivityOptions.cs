using Temporalio.Common;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Notifications.Sdk.Workflows;

/// <summary>Same per-category retry-policy shape as PaymentCallbacks.Sdk's
/// PaymentActivityOptions — duplicated rather than shared because that type lives in
/// PaymentCallbacks.Sdk, which Institution.Api/Platform.Api don't (and shouldn't) need
/// to reference just for this.</summary>
public static class NotificationActivityOptions
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

    /// <summary>A write must not be lost — retry for longer than a read before
    /// giving up.</summary>
    public static readonly ActivityOptions DatabaseWrite = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(30),
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 0,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(5),
            MaximumInterval = TimeSpan.FromMinutes(2),
        },
    };

    /// <summary>
    /// A paced run of WhatsApp messages to many people. Never retried as a whole (a retry would send the first part again);
    /// it heartbeats between messages so a stuck run is noticed, and a failed recipient is skipped inside the activity.
    /// </summary>
    public static readonly ActivityOptions WhatsAppBatch = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(15),
        HeartbeatTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 1 },
    };

    /// <summary>SMS/WhatsApp/email gateway calls — same swallow-and-log contract the
    /// gateways already have, but a slow provider shouldn't hold up the queue forever.</summary>
    public static readonly ActivityOptions ExternalGateway = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(10),
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 0,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(2),
            MaximumInterval = TimeSpan.FromMinutes(1),
        },
    };
}
