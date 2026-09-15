using System.Security.Cryptography;
using System.Text;
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ReservEase.Alumni.Member.Api.Actors;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/callbacks/paystack")]
public class PaystackCallbackController : ControllerBase
{
    private readonly ILogger<PaystackCallbackController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IActorRef _callbackActor;
    private readonly IAlumniPgRepository<WebhookEvent> _webhookEventRepo;

    public PaystackCallbackController(
        ILogger<PaystackCallbackController> logger,
        IConfiguration configuration,
        IActorRef callbackActor,
        IAlumniPgRepository<WebhookEvent> webhookEventRepo)
    {
        _logger = logger;
        _configuration = configuration;
        _callbackActor = callbackActor;
        _webhookEventRepo = webhookEventRepo;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PaystackCallback()
    {
        // Read the raw body to validate the HMAC signature.
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
        }
        Request.Body.Position = 0;

        var signature = Request.Headers["x-paystack-signature"].FirstOrDefault();
        var secretKey = _configuration["PaystackConfig:SecretKey"] ?? string.Empty;

        if (!ValidatePaystackSignature(rawBody, signature, secretKey))
        {
            _logger.LogWarning("Paystack webhook: invalid signature (IP={IP})", HttpContext.Connection.RemoteIpAddress);
             return Unauthorized(new { received = false });
        }

        PaystackCallbackModel? callback;
        try
        {
            callback = JsonConvert.DeserializeObject<PaystackCallbackModel>(rawBody);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Paystack webhook: failed to deserialize payload");
            return Ok(new { received = false });
        }

        if (callback is null)
        {
            _logger.LogWarning("Paystack webhook: payload deserialized to null");
            return Ok(new { received = false });
        }

        _logger.LogInformation("Paystack webhook received. Event={Event}, Reference={Reference}", callback.Event, callback.Data?.Reference);

        // Only act on successful charges
        if (!string.IsNullOrEmpty(callback.Data?.Reference))
        {
            // Persist the raw delivery before handing off to the actor — a crash between
            // here and the actor finishing must not lose the event; it stays in the inbox
            // as unprocessed (ProcessedAt == null) and can be replayed/audited.
            var webhookEvent = new WebhookEvent
            {
                Provider = "Paystack",
                Reference = callback.Data.Reference,
                RawBody = rawBody,
                ReceivedAt = DateTime.UtcNow,
            };
            await _webhookEventRepo.AddAsync(webhookEvent);

            var command = new ProcessPaystackCallbackCommand(callback.Data.Reference, rawBody, webhookEvent.Id);
            _callbackActor.Tell(new ConsistentHashableEnvelope(command, callback.Data.Reference));
        }

        // Always return 200 to stop Paystack retries.
        return Ok(new { received = true });
    }

    private static bool ValidatePaystackSignature(string rawBody, string? signature, string secretKey)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secretKey))
            return false;

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();

        var signatureBytes = Encoding.UTF8.GetBytes(signature.ToLowerInvariant().Trim());
        var computedBytes = Encoding.UTF8.GetBytes(computed);
        return CryptographicOperations.FixedTimeEquals(computedBytes, signatureBytes);
    }
}
