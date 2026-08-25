using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Compose and send bulk SMS / in-app broadcasts to filtered groups of members.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin,ScopedAdmin")]
public class BroadcastController(IBroadcastService broadcastService) : DefaultController
{
    /// <summary>
    /// Get a live count of members matching the given filter, before sending.
    /// </summary>
    [HttpGet("recipient-count")]
    [SwaggerOperation(Summary = "Count broadcast recipients", Description = "Preview how many members match a filter before sending.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    public async Task<IActionResult> GetRecipientCount([FromQuery] BroadcastFilter filter)
    {
        var admin = User.GetAccount();
        var result = await broadcastService.GetRecipientCountAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Send a broadcast (SMS and/or in-app) to all members matching the filter.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Send broadcast", Description = "Fan out a message to all members matching the filter via the selected channels.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BroadcastResult>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SendBroadcast([FromBody] SendBroadcastRequest request)
    {
        var admin = User.GetAccount();
        var result = await broadcastService.SendBroadcastAsync(request, admin);
        return result.ToActionResult();
    }
}
