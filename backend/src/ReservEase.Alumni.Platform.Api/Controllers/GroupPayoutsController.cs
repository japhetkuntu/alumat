using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Review group-level payout-setup submissions (see Institution.Api's
/// CommunitiesController.SubmitPayoutSetup) — approving actually creates/links the
/// Paystack subaccount; rejecting leaves the group settling through its
/// institution's own account, same as before submission.
/// </summary>
[Authorize(Roles = "SuperAdmin,Billing")]
[Route("api/v{version:apiVersion}/group-payouts")]
public class GroupPayoutsController(IGroupPayoutService groupPayoutService) : DefaultController
{
    [HttpGet("pending")]
    [SwaggerOperation(Summary = "List groups with a pending payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PendingGroupPayoutItem>>))]
    public async Task<IActionResult> GetPending()
    {
        var result = await groupPayoutService.GetPendingAsync();
        return result.ToActionResult();
    }

    [HttpPut("{communityId}/approve")]
    [SwaggerOperation(Summary = "Approve a group's payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Approve(string communityId)
    {
        var acct = User.GetAccount();
        var result = await groupPayoutService.ApproveAsync(communityId, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpPut("{communityId}/reject")]
    [SwaggerOperation(Summary = "Reject a group's payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Reject(string communityId, [FromBody] RejectGroupPayoutRequest request)
    {
        var acct = User.GetAccount();
        var result = await groupPayoutService.RejectAsync(communityId, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
