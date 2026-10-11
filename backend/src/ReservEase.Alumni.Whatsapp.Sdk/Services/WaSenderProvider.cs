using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;

namespace ReservEase.Alumni.Whatsapp.Sdk.Services;

/// <summary>
/// WaSenderAPI (https://wasenderapi.com) behind <see cref="IWhatsAppProvider"/>. Only endpoints from its published API reference
/// are used: POST /api/send-message, GET /api/status, GET /api/groups/{jid}/metadata, GET /api/pn-from-lid/{lid}. Response bodies
/// are read defensively because the reference does not publish their schemas, so a missing field degrades to "unknown", never to
/// an exception.
/// </summary>
public class WaSenderProvider(
    IOptions<WaSenderConfig> options,
    IHttpClientFactory httpClientFactory,
    ILogger<WaSenderProvider> logger) : IWhatsAppProvider
{
    private readonly WaSenderConfig config = options.Value;

    private HttpClient Client()
    {
        var client = httpClientFactory.CreateClient("WaSender");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        return client;
    }

    public async Task<WhatsAppSendResult> SendMessageAsync(WhatsAppOutboundMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client().PostAsJsonAsync($"{config.BaseUrl}/api/send-message", new { to = message.To, text = message.Text }, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // 429 and 5xx are worth another attempt later; a 4xx about the request itself will not improve.
                var retryable = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                logger.LogError("WaSender send failed ({Status}) to {To}: {Body}", response.StatusCode, Redact(message.To), body);
                return WhatsAppSendResult.Failed($"HTTP {(int)response.StatusCode}", retryable);
            }

            logger.LogInformation("WhatsApp message sent to {To}", Redact(message.To));
            return WhatsAppSendResult.Sent(ReadString(body, "data", "msgId") ?? ReadString(body, "msgId"));
        }
        catch (Exception e)
        {
            logger.LogError(e, "WaSender send threw for {To}", Redact(message.To));
            return WhatsAppSendResult.Failed(e.GetType().Name, retryable: true);
        }
    }

    public async Task<WhatsAppConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client().GetAsync($"{config.BaseUrl}/api/status", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) return WhatsAppConnectionStatus.Unreachable($"HTTP {(int)response.StatusCode}");

            var state = (ReadString(body, "status") ?? ReadString(body, "data", "status") ?? "unknown").ToLowerInvariant();
            return new WhatsAppConnectionStatus(state == "connected", state);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "WaSender status check failed");
            return WhatsAppConnectionStatus.Unreachable(e.GetType().Name);
        }
    }

    public async Task<WhatsAppGroupDetails?> GetGroupAsync(string groupJid, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client().GetAsync($"{config.BaseUrl}/api/groups/{Uri.EscapeDataString(groupJid)}/metadata", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var name = ReadString(body, "data", "subject") ?? ReadString(body, "data", "name") ?? ReadString(body, "subject");
            return name is null ? null : new WhatsAppGroupDetails(groupJid, name);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "WaSender group lookup failed");
            return null;
        }
    }

    public async Task<string?> ResolvePhoneFromLidAsync(string lid, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client().GetAsync($"{config.BaseUrl}/api/pn-from-lid/{Uri.EscapeDataString(lid)}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var raw = ReadString(body, "data", "pn") ?? ReadString(body, "pn") ?? ReadString(body, "data");
            var digits = raw is null ? null : new string(raw.Split('@')[0].Where(char.IsAsciiDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? null : digits;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "WaSender LID resolution failed");
            return null;
        }
    }

    private static string? ReadString(string json, params string[] path)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var node = doc.RootElement;
            foreach (var key in path)
            {
                if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(key, out node)) return null;
            }
            return node.ValueKind == JsonValueKind.String ? node.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    // A phone number is personal data; logs keep enough to correlate, not to dial.
    private static string Redact(string to) => to.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase) ? to : to.Length > 4 ? $"…{to[^4..]}" : "…";
}
