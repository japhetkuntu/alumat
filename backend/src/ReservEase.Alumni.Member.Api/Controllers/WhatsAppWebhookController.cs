using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Member.Api.Services.Assistant;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// Where WaSenderAPI delivers inbound WhatsApp events (a message to the AlumUnion Assistant number, a session going offline).
/// Modelled on <see cref="PaystackCallbackController"/>: no login, no tenant (the Host is the platform's, not an institution's),
/// authenticated by a shared secret, answers 200 quickly. The reply is queued through Temporal so a provider hiccup or a
/// redelivered webhook never produces a lost or doubled answer.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/callbacks/whatsapp")]
public class WhatsAppWebhookController(
    IOptions<WaSenderConfig> options,
    AssistantEngine assistant,
    GroupCommandHandler groups,
    IWhatsAppProvider provider,
    ITemporalClientProvider temporal,
    ReservEase.Alumni.PostgresDb.Sdk.Services.IPlatformWhatsAppSwitch platformSwitch,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    /// <summary>A message the provider took longer than this to deliver is stale: answering it would only confuse.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var config = options.Value;

        Request.EnableBuffering();
        string raw;
        using (var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true))
            raw = await reader.ReadToEndAsync(cancellationToken);

        if (!WaSenderWebhookAuth.IsValid(Request.Headers["X-Webhook-Signature"].ToString(), config.WebhookSecret))
        {
            logger.LogWarning("Rejected WhatsApp webhook with a missing or wrong secret");
            return Unauthorized(new { received = false });
        }

        // Switched off: acknowledge so the provider does not keep retrying, and do nothing.
        // Off in configuration, or switched off by the platform administrator: acknowledge, do nothing.
        if (!config.Enabled || !await platformSwitch.IsOnAsync()) return Ok(new { received = true, handled = false });

        var message = WaSenderWebhookParser.Parse(raw);
        switch (message.Kind)
        {
            case WhatsAppInboundKind.SessionStatus:
                if (message.SessionState is not null && message.SessionState != "connected")
                    logger.LogWarning("WhatsApp session state is now {State}; the assistant cannot reply until it reconnects", message.SessionState);
                return Ok(new { received = true });

            case WhatsAppInboundKind.PersonalMessage:
            case WhatsAppInboundKind.GroupMessage:
                break;

            default:
                return Ok(new { received = true });
        }

        // What arrived, with no content and no number: enough to tell, from the log, a menu reply the parser could not read
        // from one the assistant could not answer.
        logger.LogInformation("WhatsApp {Kind} received: hasText={HasText}, media={Media}, hasNumber={HasNumber}, fromMe={FromMe}",
            message.Kind, !string.IsNullOrWhiteSpace(message.Text), message.MediaKind, message.SenderPhone is not null, message.FromMe);

       // if (message.FromMe) return Ok(new { received = true });
        if (message.SentAt is { } sent && DateTimeOffset.UtcNow - sent > MaxAge)
        {
            logger.LogInformation("Ignoring a WhatsApp message older than {Minutes} minutes", MaxAge.TotalMinutes);
            return Ok(new { received = true, stale = true });
        }

        try
        {
            // WhatsApp sometimes reports a sender only as an anonymised id; resolve it to a number when the provider can.
            // (Groups never need the sender: commands are answered the same for everyone.)
            if (message.Kind == WhatsAppInboundKind.PersonalMessage && message.SenderPhone is null && message.SenderLid is not null)
            {
                var resolved = await provider.ResolvePhoneFromLidAsync(message.SenderLid, cancellationToken);
                if (resolved is not null) message = message with { SenderPhone = resolved };
            }

            var reply = message.Kind == WhatsAppInboundKind.GroupMessage
                ? await groups.HandleAsync(message, cancellationToken)
                : await assistant.HandleAsync(message, cancellationToken);
            if (reply is null) return Ok(new { received = true });

            var key = message.MessageId ?? Guid.NewGuid().ToString("N");
            var queued = await temporal.EnqueueWhatsAppAsync(new WhatsAppSendRequest(reply.To, reply.Text, "assistant-reply"), key, logger);

            // Temporal being down must not make the assistant go silent: one direct attempt, no retries.
            if (!queued)
                await provider.SendMessageAsync(new WhatsAppOutboundMessage(reply.To, reply.Text), cancellationToken);
        }
        catch (Exception e)
        {
            // Never let one bad message turn into a failing webhook the provider retries forever.
            logger.LogError(e, "WhatsApp assistant failed handling message {MessageId}", message.MessageId);
        }

        return Ok(new { received = true });
    }
}
