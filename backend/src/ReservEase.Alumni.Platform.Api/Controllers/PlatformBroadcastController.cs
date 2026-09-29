using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Lets platform staff send a targeted, image-capable notification (in-app / SMS / email) to a
/// filtered group of one institution's own members — the platform-side equivalent of
/// Institution.Api's BroadcastController, for support/onboarding outreach on an institution's
/// behalf (e.g. nudging members who've gone quiet, or haven't given to a live fundraiser).
/// </summary>
[Authorize(Roles = "SuperAdmin,Support")]
[Route("api/v{version:apiVersion}/broadcast")]
public class PlatformBroadcastController(IPlatformBroadcastService broadcastService) : DefaultController
{
    [HttpGet("recipient-count")]
    [SwaggerOperation(Summary = "Count broadcast recipients", Description = "Preview how many of one institution's members match a filter before sending.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    public async Task<IActionResult> GetRecipientCount([FromQuery] PlatformBroadcastFilter filter)
    {
        var result = await broadcastService.GetRecipientCountAsync(filter);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Send broadcast", Description = "Fan out a message to one institution's members matching the filter, via the selected channels. Upload an image first via POST /uploads/image and pass its URL.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformBroadcastResult>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SendBroadcast([FromBody] SendPlatformBroadcastRequest request)
    {
        var admin = User.GetAccount();
        var result = await broadcastService.SendBroadcastAsync(request, admin);
        return result.ToActionResult();
    }
}
