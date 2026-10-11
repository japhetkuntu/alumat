namespace ReservEase.Alumni.Whatsapp.Sdk.Models;

/// <summary>A text message to a person (phone digits with country code) or a group (its JID, ending "@g.us").</summary>
public sealed record WhatsAppOutboundMessage(string To, string Text)
{
    public bool IsGroup => To.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// What a send attempt came to. <see cref="Retryable"/> separates "try again later" (provider down, rate limited, session
/// reconnecting) from "will never work" (bad number), so callers can decide whether another attempt is worth making.
/// </summary>
public sealed record WhatsAppSendResult(bool Success, string? MessageId = null, bool Retryable = false, string? Error = null)
{
    public static WhatsAppSendResult Sent(string? messageId = null) => new(true, messageId);
    public static WhatsAppSendResult Failed(string error, bool retryable) => new(false, null, retryable, error);
}

public sealed record WhatsAppConnectionStatus(bool Connected, string State, string? Detail = null)
{
    public static WhatsAppConnectionStatus Unreachable(string detail) => new(false, "unreachable", detail);
}

public sealed record WhatsAppGroupDetails(string Jid, string Name, int? ParticipantCount = null);

public enum WhatsAppInboundKind
{
    /// <summary>Anything the assistant does not act on (reactions, receipts, calls, other session events).</summary>
    Ignored,
    PersonalMessage,
    GroupMessage,
    SessionStatus,
}

/// <summary>
/// A message the provider reports as received, reduced to what the assistant needs. The text is processed and discarded:
/// nothing here is stored. <see cref="SenderPhone"/> is digits with country code, taken from the provider's own sender field,
/// never from message content.
/// </summary>
public sealed record WhatsAppInboundEvent(
    WhatsAppInboundKind Kind,
    string? MessageId = null,
    string? SenderPhone = null,
    string? SenderLid = null,
    string? ChatId = null,
    string? Text = null,
    DateTimeOffset? SentAt = null,
    bool FromMe = false,
    string? SessionState = null,
    string? MediaKind = null)
{
    /// <summary>Where a reply goes: the group for a group message, otherwise the sender's number.</summary>
    public string? ReplyTo => Kind == WhatsAppInboundKind.GroupMessage ? ChatId : SenderPhone ?? ChatId;
}
