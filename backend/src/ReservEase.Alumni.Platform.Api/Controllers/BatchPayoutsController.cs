using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Review batch-level payout-setup submissions (see Institution.Api's
/// BatchesController.SubmitPayoutSetup) — approving actually creates/links the
/// Paystack subaccount; rejecting leaves the batch settling through its
/// institution's own account, same as before submission.
/// </summary>
[Authorize(Roles = "SuperAdmin,Billing")]
[Route("api/v{version:apiVersion}/batch-payouts")]
public class BatchPayoutsController(IBatchPayoutService batchPayoutService) : DefaultController
{
    [HttpGet("pending")]
    [SwaggerOperation(Summary = "List batches with a pending payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PendingBatchPayoutItem>>))]
    public async Task<IActionResult> GetPending()
    {
        var result = await batchPayoutService.GetPendingAsync();
        return result.ToActionResult();
    }

    [HttpPut("{batchId}/approve")]
    [SwaggerOperation(Summary = "Approve a batch's payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Approve(string batchId)
    {
        var acct = User.GetAccount();
        var result = await batchPayoutService.ApproveAsync(batchId, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpPut("{batchId}/reject")]
    [SwaggerOperation(Summary = "Reject a batch's payout setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Reject(string batchId, [FromBody] RejectBatchPayoutRequest request)
    {
        var acct = User.GetAccount();
        var result = await batchPayoutService.RejectAsync(batchId, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
