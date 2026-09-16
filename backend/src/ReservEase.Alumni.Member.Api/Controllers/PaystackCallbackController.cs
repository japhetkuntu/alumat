using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Member.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/callbacks/paystack")]
public class PaystackCallbackController : ControllerBase
{
    private readonly ILogger<PaystackCallbackController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ITemporalClientProvider _temporalProvider;
    private readonly IStoreOrderService _storeOrderService;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IContributionService _contributionService;

    public PaystackCallbackController(
        ILogger<PaystackCallbackController> logger,
        IConfiguration configuration,
        ITemporalClientProvider temporalProvider,
        IStoreOrderService storeOrderService,
        IServiceRequestService serviceRequestService,
        IContributionService contributionService)
    {
        _logger = logger;
        _configuration = configuration;
        _temporalProvider = temporalProvider;
        _storeOrderService = storeOrderService;
        _serviceRequestService = serviceRequestService;
        _contributionService = contributionService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
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

        var reference = callback.Data?.Reference;
        _logger.LogInformation("Paystack webhook received. Event={Event}, Reference={Reference}", callback.Event, reference);

        // Only act on successful charges
        if (!string.IsNullOrEmpty(reference))
        {
            if (!_temporalProvider.IsAvailable)
            {
                _logger.LogError("Temporal unavailable; cannot accept Paystack webhook for {Reference}", reference);
                // Non-200 so Paystack's own webhook retry becomes the safety net — there is
                // no local inbox to fall back on once Temporal owns durability for this flow.
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { received = false });
            }

            var unavailable = await RouteToWorkflowAsync(reference, rawBody);
            if (unavailable is not null)
                return unavailable;
        }

        // Always return 200 to stop Paystack retries once the workflow has started.
        return Ok(new { received = true });
    }

    /// <summary>
    /// Each domain (contribution, service request, store order) gets its own workflow type
    /// and its own workflow ID namespace, so Temporal's history/visibility separates them
    /// cleanly — this is purely a routing decision, no processing happens here. References
    /// minted after the prefix scheme shipped (SO_/SR_/CN_) route directly; an unprefixed
    /// reference predates that scheme and falls back to the old guess-by-existence-check.
    /// Returns a 503 result if Temporal turns out to be unreachable mid-routing, else null.
    /// </summary>
    private async Task<IActionResult?> RouteToWorkflowAsync(string reference, string rawBody)
    {
        var prefix = PaystackReferencePrefix.ExtractPrefix(reference);
        switch (prefix)
        {
            case PaystackReferencePrefix.Contribution:
                return await StartContributionWorkflowAsync(reference, rawBody);

            case PaystackReferencePrefix.ServiceRequest:
                return await StartServiceRequestWorkflowAsync(reference, rawBody);

            case PaystackReferencePrefix.StoreOrder:
                return await StartStoreOrderWorkflowAsync(reference, rawBody);
        }

        if (await _contributionService.OwnsReferenceAsync(reference))
            return await StartContributionWorkflowAsync(reference, rawBody);

        if (await _serviceRequestService.OwnsReferenceAsync(reference))
            return await StartServiceRequestWorkflowAsync(reference, rawBody);

        if (await _storeOrderService.OwnsReferenceAsync(reference))
            return await StartStoreOrderWorkflowAsync(reference, rawBody);

        _logger.LogWarning("Paystack webhook for reference {Reference} was not claimed by any service", reference);
        return null;
    }

    private Task<IActionResult?> StartContributionWorkflowAsync(string reference, string rawBody) =>
        StartWorkflowAsync(
            $"payment-callback-contribution-{reference}",
            reference,
            (IProcessContributionCallbackWorkflow wf) => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, rawBody)));

    private Task<IActionResult?> StartServiceRequestWorkflowAsync(string reference, string rawBody) =>
        StartWorkflowAsync(
            $"payment-callback-servicerequest-{reference}",
            reference,
            (IProcessServiceRequestCallbackWorkflow wf) => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, rawBody)));

    private Task<IActionResult?> StartStoreOrderWorkflowAsync(string reference, string rawBody) =>
        StartWorkflowAsync(
            $"payment-callback-storeorder-{reference}",
            reference,
            (IProcessStoreOrderCallbackWorkflow wf) => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, rawBody)));

    private async Task<IActionResult?> StartWorkflowAsync<TWorkflow>(
        string workflowId, string reference, System.Linq.Expressions.Expression<Func<TWorkflow, Task>> runCall)
        where TWorkflow : class
    {
        try
        {
            await _temporalProvider.Client!.StartWorkflowAsync(
                runCall,
                new WorkflowOptions
                {
                    Id = workflowId,
                    TaskQueue = OperationsTaskQueues.PaymentCallbackProcessing,
                    // AllowDuplicate, not RejectDuplicate: a closed workflow here doesn't mean
                    // "already recorded" — the domain logic can legitimately no-op (e.g. Paystack
                    // reference metadata not yet cached, campaign lookup transiently unavailable)
                    // and still complete successfully. RejectDuplicate would permanently block
                    // Paystack's own later retry of that reference from ever being reprocessed.
                    // AllowDuplicate still refuses to start a second execution while one for this
                    // reference is actively RUNNING, so concurrent redeliveries can't race each
                    // other — real double-processing protection is the domain-level unique
                    // TransactionRef constraint (PaymentTransaction/StoreOrder/ServiceRequest),
                    // not the workflow ID.
                    IdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate,
                });
        }
        catch (WorkflowAlreadyStartedException)
        {
            // A workflow for this reference is still actively running — this redelivery
            // arrived mid-flight; no-op rather than racing a second execution against it.
            _logger.LogInformation("Paystack webhook redelivery for {Reference} — workflow already running", reference);
        }
        catch (RpcException ex) when (ex.Code is RpcException.StatusCode.Unavailable or RpcException.StatusCode.DeadlineExceeded)
        {
            _logger.LogError(ex, "Temporal unreachable while starting workflow {WorkflowId}", workflowId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { received = false });
        }

        return null;
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
