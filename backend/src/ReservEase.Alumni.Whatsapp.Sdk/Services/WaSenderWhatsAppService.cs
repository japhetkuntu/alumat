using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Whatsapp.Sdk.Models;

namespace ReservEase.Alumni.Whatsapp.Sdk.Services;

/// <summary>
/// The original fire-and-forget send used by the notification workflow. Now a thin adapter over <see cref="IWhatsAppProvider"/>
/// so there is one place that talks to WaSender; behaviour is unchanged (true only on success, never throws).
/// </summary>
public class WaSenderWhatsAppService(IWhatsAppProvider provider, ILogger<WaSenderWhatsAppService> logger) : IWhatsAppService
{
    public async Task<bool> SendMessageAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var result = await provider.SendMessageAsync(new WhatsAppOutboundMessage(phoneNumber, message), cancellationToken);
        if (!result.Success) logger.LogWarning("WhatsApp message not sent ({Error}, retryable: {Retryable})", result.Error, result.Retryable);
        return result.Success;
    }
}
