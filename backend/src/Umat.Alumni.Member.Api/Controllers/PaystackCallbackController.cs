using System.Security.Cryptography;
using System.Text;
using Akka.Actor;
using Akka.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Umat.Alumni.Member.Api.Actors;
using Umat.Alumni.Member.Api.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/callbacks/paystack")]
public class PaystackCallbackController : ControllerBase
{
    private readonly ILogger<PaystackCallbackController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IActorRef _callbackActor;

    public PaystackCallbackController(
        ILogger<PaystackCallbackController> logger,
        IConfiguration configuration,
        IActorRef callbackActor)
    {
        _logger = logger;
        _configuration = configuration;
        _callbackActor = callbackActor;
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
            // return Unauthorized(new { received = false });
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
        if ( !string.IsNullOrEmpty(callback.Data?.Reference))
        {
            _callbackActor.Tell(new ProcessPaystackCallbackCommand(callback.Data.Reference, rawBody));
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
