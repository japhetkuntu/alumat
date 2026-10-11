using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Assistant;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// The signed-in member's own WhatsApp connection. The member is always the caller's account (taken from the token): no id is
/// ever accepted from the client, so one member can neither start nor end another's connection.
/// </summary>
[Authorize]
public class WhatsAppController(WhatsAppLinkService links, WhatsAppGroupDirectory groups) : DefaultController
{
    [HttpGet("groups")]
    [SwaggerOperation(Summary = "WhatsApp groups I can join", Description = "Groups an administrator has connected and shared an invitation for, limited to the member's own communities")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IReadOnlyList<JoinableWhatsAppGroup>>))]
    public async Task<IActionResult> Groups()
        => (await groups.ForMemberAsync(User.GetAccount().Id)).ToOkApiResponse().ToActionResult();

    [HttpGet]
    [SwaggerOperation(Summary = "WhatsApp connection status", Description = "Whether this member's WhatsApp is connected, and the assistant's number")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppLinkStatus>))]
    public async Task<IActionResult> Status()
        => (await links.GetStatusAsync(User.GetAccount().Id)).ToOkApiResponse().ToActionResult();

    [HttpPost("link")]
    [SwaggerOperation(Summary = "Start connecting WhatsApp", Description = "Creates a one-time code (valid 10 minutes). Sending it to the assistant from the WhatsApp account to connect completes the link")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppLinkChallenge>))]
    public async Task<IActionResult> StartLink()
        => (await links.CreateChallengeAsync(User.GetAccount().Id)).ToOkApiResponse().ToActionResult();

    [HttpPost("link/approve")]
    [SwaggerOperation(Summary = "Approve the number that sent the code", Description = "Needed when the code came from a number other than the one on this account")]
    public async Task<IActionResult> Approve()
    {
        var outcome = await links.ApproveAsync(User.GetAccount().Id);
        return outcome.Status switch
        {
            LinkStatus.Linked => new object().ToOkApiResponse("WhatsApp connected.").ToActionResult(),
            LinkStatus.Conflict => ApiResponseExtensions.ToConflictApiResponse<object>("That number is already connected to another account here.").ToActionResult(),
            _ => ApiResponseExtensions.ToBadRequestApiResponse<object>("There is nothing waiting for approval. Start again.").ToActionResult(),
        };
    }

    [HttpDelete("link/pending")]
    [SwaggerOperation(Summary = "Refuse the number that sent the code")]
    public async Task<IActionResult> RefusePending()
    {
        await links.RejectPendingAsync(User.GetAccount().Id);
        return new object().ToOkApiResponse("Refused. Nothing was connected.").ToActionResult();
    }

    [HttpDelete]
    [SwaggerOperation(Summary = "Disconnect WhatsApp", Description = "Removes the connection and stops WhatsApp notifications for this member")]
    public async Task<IActionResult> Unlink()
    {
        var removed = await links.UnlinkMemberAsync(User.GetAccount().Id);
        return new object().ToOkApiResponse(removed ? "WhatsApp disconnected." : "WhatsApp was not connected.").ToActionResult();
    }
}
