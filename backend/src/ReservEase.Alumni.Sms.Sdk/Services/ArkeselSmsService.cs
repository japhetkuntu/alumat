using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Sms.Sdk.Options;

namespace ReservEase.Alumni.Sms.Sdk.Services;

/// <summary>
/// Sends SMS via Arkesel's v2 API (https://developers.arkesel.com/) — the
/// SMS provider for this platform's African-market institutions.
/// </summary>
public class ArkeselSmsService(
    IOptions<ArkeselConfig> options,
    IHttpClientFactory httpClientFactory,
    ILogger<ArkeselSmsService> logger) : ISmsService
{
    private readonly ArkeselConfig config = options.Value;

    public async Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Arkesel");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Remove("api-key");
            client.DefaultRequestHeaders.Add("api-key", config.ApiKey);

            var payload = new
            {
                sender = config.SenderId,
                message,
                recipients = new[] { phoneNumber },
            };

            var response = await client.PostAsJsonAsync($"{config.BaseUrl}/api/v2/sms/send", payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Arkesel SMS send failed ({Status}) to {Phone}: {Body}", response.StatusCode, phoneNumber, body);
                return false;
            }

            logger.LogInformation("SMS sent to {Phone}", phoneNumber);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Arkesel SMS send threw for {Phone}", phoneNumber);
            return false;
        }
    }
}
