using Temporalio.Common;
using Temporalio.Workflows;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Shared per-category retry policies for payment-callback activities, reused across
/// all three workflows instead of each redefining its own ActivityOptions — mirrors
/// Hubtel LendScore's ActivityOptionService pattern.
/// </summary>
public static class PaymentActivityOptions
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

    /// <summary>Financial-critical: keep retrying for up to 2 hours (a downstream
    /// Paystack outage shouldn't lose a payment callback) with exponential backoff.</summary>
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

    /// <summary>A failed SMS/WhatsApp/in-app notification must never block the
    /// payment itself from being recorded — runs after the DB write, shorter
    /// budget than the writes it follows.</summary>
    public static readonly ActivityOptions Notification = new()
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
