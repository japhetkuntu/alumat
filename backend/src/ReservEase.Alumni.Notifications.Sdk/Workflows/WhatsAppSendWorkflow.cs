using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Notifications.Sdk.Workflows;

/// <summary>One WhatsApp message to send: a person's number (digits with country code) or a group's JID.</summary>
public sealed record WhatsAppSendRequest(string To, string Text, string? Reason = null);

/// <summary>Its own queue, so a slow WhatsApp session can never delay payment callbacks or other notifications.</summary>
public static class WhatsAppTaskQueues
{
    public const string Send = "operations-whatsapp";
}

/// <summary>
/// Durable delivery of exactly one WhatsApp message, implemented by Operations.Worker. The assistant and the admin publishing
/// flow start it and move on; the workflow retries a provider that is briefly unreachable and (by its queue's concurrency of one
/// plus a pause per send) keeps the single WhatsApp session from being driven faster than a person plausibly would.
/// </summary>
[Workflow("WhatsAppSend")]
public interface IWhatsAppSendWorkflow
{
    [WorkflowRun]
    Task RunAsync(WhatsAppSendRequest request);
}

public static class WhatsAppSendClientExtensions
{
    /// <summary>
    /// Queues a message. <paramref name="idempotencyKey"/> (for a reply: the inbound message's id) becomes the workflow id with
    /// reject-duplicate reuse, so a webhook the provider delivers twice produces one reply, not two. Never throws: an unavailable
    /// Temporal means the message is dropped and logged, the same contract as notifications.
    /// </summary>
    public static async Task<bool> EnqueueWhatsAppAsync(
        this ITemporalClientProvider provider, WhatsAppSendRequest request, string idempotencyKey, ILogger logger)
    {
        if (!provider.IsAvailable)
        {
            logger.LogWarning("Temporal unavailable: dropped WhatsApp message {Key}", idempotencyKey);
            return false;
        }

        try
        {
            await provider.Client!.StartWorkflowAsync(
                (IWhatsAppSendWorkflow wf) => wf.RunAsync(request),
                new WorkflowOptions
                {
                    Id = $"wa-out-{idempotencyKey}",
                    TaskQueue = WhatsAppTaskQueues.Send,
                    IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                });
            return true;
        }
        catch (WorkflowAlreadyStartedException)
        {
            logger.LogInformation("WhatsApp message {Key} already queued; ignoring the repeat", idempotencyKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue WhatsApp message {Key}", idempotencyKey);
            return false;
        }
    }
}
