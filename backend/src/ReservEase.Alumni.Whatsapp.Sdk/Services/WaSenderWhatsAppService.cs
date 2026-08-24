using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Options;

namespace ReservEase.Alumni.Whatsapp.Sdk.Services;

/// <summary>
/// Sends WhatsApp messages via WaSenderAPI (https://wasenderapi.com/).
/// </summary>
public class WaSenderWhatsAppService(
    IOptions<WaSenderConfig> options,
    IHttpClientFactory httpClientFactory,
    ILogger<WaSenderWhatsAppService> logger) : IWhatsAppService
{
    private readonly WaSenderConfig config = options.Value;

    public async Task<bool> SendMessageAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("WaSender");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            var payload = new { to = phoneNumber, text = message };

            var response = await client.PostAsJsonAsync($"{config.BaseUrl}/api/send-message", payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("WaSender WhatsApp send failed ({Status}) to {Phone}: {Body}", response.StatusCode, phoneNumber, body);
                return false;
            }

            logger.LogInformation("WhatsApp message sent to {Phone}", phoneNumber);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "WaSender WhatsApp send threw for {Phone}", phoneNumber);
            return false;
        }
    }
}
