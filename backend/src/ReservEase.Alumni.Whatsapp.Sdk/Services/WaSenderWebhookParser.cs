using System.Text.Json;
using ReservEase.Alumni.Whatsapp.Sdk.Models;

namespace ReservEase.Alumni.Whatsapp.Sdk.Services;

/// <summary>
/// Turns a WaSender webhook body into <see cref="WhatsAppInboundEvent"/>. Field paths come from WaSender's published webhook
/// pages for "messages-personal.received" and "messages-group.received": data.messages.key.{id, fromMe, remoteJid, senderPn,
/// cleanedSenderPn, senderLid, participant, cleanedParticipantPn}, data.messages.messageBody. Anything unrecognised parses to
/// <see cref="WhatsAppInboundKind.Ignored"/>; the parser never throws.
/// </summary>
public static class WaSenderWebhookParser
{
    public static WhatsAppInboundEvent Parse(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var ev = Str(root, "event") ?? "";
            var data = Obj(root, "data");

            if (ev.StartsWith("session.status", StringComparison.OrdinalIgnoreCase) || ev.Equals("session-status", StringComparison.OrdinalIgnoreCase))
                return new WhatsAppInboundEvent(WhatsAppInboundKind.SessionStatus, SessionState: Str(data, "status")?.ToLowerInvariant());

            var kind = ev.Equals("messages-personal.received", StringComparison.OrdinalIgnoreCase) ? WhatsAppInboundKind.PersonalMessage
                     : ev.Equals("messages-group.received", StringComparison.OrdinalIgnoreCase) ? WhatsAppInboundKind.GroupMessage
                     : WhatsAppInboundKind.Ignored;
            if (kind == WhatsAppInboundKind.Ignored) return new WhatsAppInboundEvent(WhatsAppInboundKind.Ignored);

            // "messages" is an object in the published examples; accept a one-element array too.
            var messages = Obj(data, "messages");
            if (messages is null && data is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                messages = arr[0];
            if (messages is null) return new WhatsAppInboundEvent(WhatsAppInboundKind.Ignored);

            var key = Obj(messages, "key");
            var text = ReadText(messages);
            var remoteJid = Str(key, "remoteJid");
            // The chat's own address is the truth about where a message was posted: a group's ends "@g.us". Trust it over the
            // event name, so a group message is never mistaken for a private one (where a group code would be refused).
            if (remoteJid?.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase) == true) kind = WhatsAppInboundKind.GroupMessage;
            else if (kind == WhatsAppInboundKind.GroupMessage && remoteJid is not null) kind = WhatsAppInboundKind.PersonalMessage;
            var fromMe = key is { ValueKind: JsonValueKind.Object } k && k.TryGetProperty("fromMe", out var f) && f.ValueKind == JsonValueKind.True;
            var sentAt = ReadTime(root, "timestamp") ?? ReadTime(messages, "messageTimestamp");

            string? phone, lid;
            if (kind == WhatsAppInboundKind.GroupMessage)
            {
                phone = Digits(Str(key, "cleanedParticipantPn") ?? Str(key, "participantPn"));
                lid = Str(key, "participantLid") ?? LidOrNull(Str(key, "participant"));
            }
            else
            {
                phone = Digits(Str(key, "cleanedSenderPn") ?? Str(key, "senderPn"))
                        ?? (remoteJid?.EndsWith("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase) == true ? Digits(remoteJid) : null);
                lid = Str(key, "senderLid") ?? LidOrNull(remoteJid);
            }

            var media = string.IsNullOrWhiteSpace(text) ? MediaKindOf(messages) : null;
            return new WhatsAppInboundEvent(kind, Str(key, "id"), phone, lid, remoteJid, text, sentAt, fromMe, MediaKind: media);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            // Whatever the provider sends, a message we cannot read is one we ignore: never a 500 the provider will redeliver.
            return new WhatsAppInboundEvent(WhatsAppInboundKind.Ignored);
        }
    }

    /// <summary>
    /// What a message without text carried: "voice", "image", "video", "document", "location", "contact", "sticker", or "other".
    /// Only the kind is read; the content is never fetched or kept. Null when there is nothing but empty text.
    /// </summary>
    private static string? MediaKindOf(JsonElement? messages)
    {
        var body = Obj(messages, "message");
        if (body is not { ValueKind: JsonValueKind.Object } b) return null;
        foreach (var prop in b.EnumerateObject())
        {
            var kind = prop.Name.ToLowerInvariant() switch
            {
                "audiomessage" or "pttmessage" => "voice",
                "imagemessage" => "image",
                "videomessage" or "ptvmessage" => "video",
                "documentmessage" or "documentwithcaptionmessage" => "document",
                "locationmessage" or "livelocationmessage" => "location",
                "contactmessage" or "contactsarraymessage" => "contact",
                "stickermessage" => "sticker",
                "messagecontextinfo" or "senderkeydistributionmessage" or "protocolmessage" => null,
                _ => "other",
            };
            if (kind is not null) return kind;
        }
        return null;
    }

    /// <summary>
    /// The words a person typed, wherever WhatsApp put them. A plain message is "messageBody"/"conversation", but swiping to
    /// *reply* to the assistant's menu (what most people do) wraps the text in "extendedTextMessage", a picture or file carries
    /// its words as a "caption", and a tapped button or list row carries its choice. Missing these makes a menu reply look like
    /// an unreadable message.
    /// </summary>
    private static string? ReadText(JsonElement? messages)
    {
        var direct = Str(messages, "messageBody");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        var body = Obj(messages, "message");
        var candidates = new[]
        {
            Str(body, "conversation"),
            Str(Obj(body, "extendedTextMessage"), "text"),
            Str(Obj(body, "imageMessage"), "caption"),
            Str(Obj(body, "videoMessage"), "caption"),
            Str(Obj(body, "documentMessage"), "caption"),
            Str(Obj(body, "buttonsResponseMessage"), "selectedDisplayText"),
            Str(Obj(body, "buttonsResponseMessage"), "selectedButtonId"),
            Str(Obj(body, "listResponseMessage"), "title"),
            Str(Obj(Obj(body, "listResponseMessage"), "singleSelectReply"), "selectedRowId"),
            Str(Obj(body, "templateButtonReplyMessage"), "selectedDisplayText"),
        };
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }

    /// <summary>
    /// A Unix time as WaSender sends it. It is seconds in the documentation but milliseconds in live deliveries, and may arrive as a
    /// number or a numeric string: anything above 10^11 is milliseconds (that is the year 5138 in seconds). Unreadable or
    /// out-of-range values give null, which the webhook treats as "no timestamp" rather than failing.
    /// </summary>
    private static DateTimeOffset? ReadTime(JsonElement? parent, string name)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } p || !p.TryGetProperty(name, out var v)) return null;
        long raw;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) raw = n;
        else if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var parsed)) raw = parsed;
        else return null;
        if (raw <= 0) return null;
        try { return raw > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(raw) : DateTimeOffset.FromUnixTimeSeconds(raw); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static JsonElement? Obj(JsonElement? parent, string name)
        => parent is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static string? Str(JsonElement? parent, string name)
        => parent is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Digits(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var d = new string(raw.Split('@')[0].Where(char.IsAsciiDigit).ToArray());
        return d.Length == 0 ? null : d;
    }

    private static string? LidOrNull(string? jid) => jid?.EndsWith("@lid", StringComparison.OrdinalIgnoreCase) == true ? jid : null;
}

public static class WaSenderWebhookAuth
{
    /// <summary>
    /// WaSender authenticates a webhook with a shared secret in the X-Webhook-Signature header (it is not a signature over the
    /// body). Compared in constant time so the secret cannot be recovered by timing. An empty configured secret never matches.
    /// </summary>
    public static bool IsValid(string? headerValue, string? configuredSecret)
    {
        if (string.IsNullOrEmpty(configuredSecret) || string.IsNullOrEmpty(headerValue)) return false;
        var a = System.Text.Encoding.UTF8.GetBytes(headerValue);
        var b = System.Text.Encoding.UTF8.GetBytes(configuredSecret);
        return a.Length == b.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }
}
