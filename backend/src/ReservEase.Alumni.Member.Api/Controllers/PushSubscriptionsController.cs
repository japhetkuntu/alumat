using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.WebPush.Sdk.Options;

namespace ReservEase.Alumni.Member.Api.Controllers;

public record PushSubscriptionKeysRequest(string P256dh, string Auth);
public record PushSubscriptionRequest(string Endpoint, PushSubscriptionKeysRequest Keys, string? UserAgent);
public record UnsubscribeRequest(string Endpoint);
public record VapidPublicKeyResponse(string PublicKey);

[Authorize]
public class PushSubscriptionsController(
    IAlumniPgRepository<PushSubscription> pushSubscriptionRepo,
    IOptions<WebPushConfig> webPushConfig) : DefaultController
{
    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Get the VAPID public key", Description = "Not a secret — the browser sends this to the push service when subscribing.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<VapidPublicKeyResponse>))]
    public IActionResult GetVapidPublicKey()
    {
        var result = new VapidPublicKeyResponse(webPushConfig.Value.VapidPublicKey);
        return result.ToOkApiResponse().ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Register a push subscription", Description = "Upserts a Web Push subscription for the current member's browser/device.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request)
    {
        var member = User.GetAccount();

        var existing = await pushSubscriptionRepo.GetOneAsync(s =>
            s.OwnerId == member.Id && s.OwnerType == PushSubscriptionOwnerTypes.Member && s.Endpoint == request.Endpoint);

        if (existing is null)
        {
            await pushSubscriptionRepo.AddAsync(new PushSubscription
            {
                OwnerId = member.Id,
                OwnerType = PushSubscriptionOwnerTypes.Member,
                Endpoint = request.Endpoint,
                P256dhKey = request.Keys.P256dh,
                AuthKey = request.Keys.Auth,
                UserAgent = request.UserAgent,
                CreatedBy = member.Id,
            });
        }
        else
        {
            existing.P256dhKey = request.Keys.P256dh;
            existing.AuthKey = request.Keys.Auth;
            existing.UserAgent = request.UserAgent;
            existing.IsActive = true;
            existing.UpdatedBy = member.Id;
            await pushSubscriptionRepo.UpdateAsync(existing);
        }

        return ((object?)null).ToOkApiResponse("Subscribed.").ToActionResult();
    }

    [HttpDelete]
    [SwaggerOperation(Summary = "Remove a push subscription", Description = "Deactivates the current member's push subscription for the given endpoint.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        var member = User.GetAccount();

        var existing = await pushSubscriptionRepo.GetOneAsync(s =>
            s.OwnerId == member.Id && s.OwnerType == PushSubscriptionOwnerTypes.Member && s.Endpoint == request.Endpoint);
        if (existing is not null)
        {
            existing.IsActive = false;
            existing.UpdatedBy = member.Id;
            await pushSubscriptionRepo.UpdateAsync(existing);
        }

        return ((object?)null).ToOkApiResponse("Unsubscribed.").ToActionResult();
    }
}
