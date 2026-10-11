using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Notifications;

[Workflow("WhatsAppSend")]
public class WhatsAppSendWorkflow : IWhatsAppSendWorkflow
{
    private static readonly ActivityOptions Send = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(20),
        StartToCloseTimeout = TimeSpan.FromSeconds(60),
        RetryPolicy = new RetryPolicy
        {
            // A provider that is down or rate limiting deserves a few patient attempts, not an endless loop of messages to a
            // number on a session that may be on the edge of a restriction.
            MaximumAttempts = 5,
            BackoffCoefficient = 2.0f,
            InitialInterval = TimeSpan.FromSeconds(10),
            MaximumInterval = TimeSpan.FromMinutes(3),
        },
    };

    [WorkflowRun]
    public Task RunAsync(WhatsAppSendRequest request)
        => Workflow.ExecuteActivityAsync((WhatsAppSendActivities a) => a.SendAsync(request.To, request.Text), Send);
}

/// <summary>
/// Sends through <see cref="IWhatsAppProvider"/>. Runs on a queue with one activity slot, and pauses a moment after each send,
/// so the single shared session sends at a human-plausible pace however many messages are queued.
/// </summary>
public class WhatsAppSendActivities(IWhatsAppProvider provider, IConfiguration configuration, ILogger<WhatsAppSendActivities> logger)
{
    /// <summary>Lower bound and spread of the pause between sends, in milliseconds. Configurable so tests do not wait.</summary>
    private int PaceMs => configuration.GetValue("WhatsAppAssistant:PaceMilliseconds", 2500);

    [Activity("WhatsAppSend.Send")]
    public async Task<bool> SendAsync(string to, string text)
    {
        if (!configuration.GetValue("WaSenderConfig:Enabled", false))
        {
            logger.LogInformation("WhatsApp disabled by configuration; message not sent");
            return false;
        }

        var ct = ActivityExecutionContext.HasCurrent ? ActivityExecutionContext.Current.CancellationToken : CancellationToken.None;
        var result = await provider.SendMessageAsync(new WhatsAppOutboundMessage(to, text), ct);
        if (PaceMs > 0) await Task.Delay(PaceMs + Random.Shared.Next(0, PaceMs / 2 + 1), ct);

        if (result.Success) return true;
        // Retryable failures are thrown so Temporal's policy retries them; a failure that will never work ends quietly.
        if (result.Retryable) throw new ApplicationFailureException($"WhatsApp send failed and may succeed later: {result.Error}");
        logger.LogWarning("WhatsApp send failed permanently: {Error}", result.Error);
        return false;
    }
}
