using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Review institution-level payout-setup submissions (see Institution.Api's
/// InstitutionController.SubmitPayoutSetup) — approving actually creates/links
/// the Paystack subaccount; rejecting leaves the institution's previous live
/// settlement fields (if any) unchanged.
/// </summary>
[Authorize(Roles = "SuperAdmin,Billing")]
[Route("api/v{version:apiVersion}/institution-payouts")]
public class InstitutionPayoutsController(IInstitutionPayoutService institutionPayoutService) : DefaultController
{
    [HttpGet("pending")]
    [SwaggerOperation(Summary = "List institutions with a pending payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PendingInstitutionPayoutItem>>))]
    public async Task<IActionResult> GetPending()
    {
        var result = await institutionPayoutService.GetPendingAsync();
        return result.ToActionResult();
    }

    [HttpPut("{institutionId}/approve")]
    [SwaggerOperation(Summary = "Approve an institution's payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Approve(string institutionId)
    {
        var acct = User.GetAccount();
        var result = await institutionPayoutService.ApproveAsync(institutionId, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpPut("{institutionId}/reject")]
    [SwaggerOperation(Summary = "Reject an institution's payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Reject(string institutionId, [FromBody] RejectInstitutionPayoutRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionPayoutService.RejectAsync(institutionId, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
