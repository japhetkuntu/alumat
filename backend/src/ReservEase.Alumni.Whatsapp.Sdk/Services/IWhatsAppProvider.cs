using ReservEase.Alumni.Whatsapp.Sdk.Models;

namespace ReservEase.Alumni.Whatsapp.Sdk.Services;

/// <summary>
/// What AlumUnion needs from a WhatsApp provider, in AlumUnion's own terms. Nothing outside the provider implementation sees a
/// WaSender request or response shape, so the official WhatsApp Cloud API (or another provider) can replace it without
/// touching the assistant, the notification workflow or the portals. Implementations never throw for provider problems; they
/// return a result that says whether trying again is worthwhile.
/// </summary>
public interface IWhatsAppProvider
{
    Task<WhatsAppSendResult> SendMessageAsync(WhatsAppOutboundMessage message, CancellationToken cancellationToken = default);

    Task<WhatsAppConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>A group the assistant is a member of, or null when it is not (or the provider cannot say).</summary>
    Task<WhatsAppGroupDetails?> GetGroupAsync(string groupJid, CancellationToken cancellationToken = default);

    /// <summary>Resolves WhatsApp's anonymised sender id ("…@lid") to a phone number's digits, when the provider can.</summary>
    Task<string?> ResolvePhoneFromLidAsync(string lid, CancellationToken cancellationToken = default);
}
