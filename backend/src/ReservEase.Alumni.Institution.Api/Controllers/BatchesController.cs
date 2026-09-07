using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage this institution's graduating-class year groups ("batches") — lets
/// admins define their own list instead of the platform's generic year range.
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/batches")]
public class BatchesController(IBatchService batchService, IPaystackService paystackService) : DefaultController
{
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,ScopedAdmin")]
    [SwaggerOperation(Summary = "List batches")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<BatchListItem>>))]
    public async Task<IActionResult> GetBatches()
    {
        var result = await batchService.GetBatchesAsync();
        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [SwaggerOperation(Summary = "Create a batch")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<BatchListItem>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest request)
    {
        var actor = User.GetAccount();
        var result = await batchService.CreateBatchAsync(request, actor.Id);
        return result.ToActionResult();
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    [SwaggerOperation(Summary = "Update a batch")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BatchListItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateBatch(string id, [FromBody] UpdateBatchRequest request)
    {
        var actor = User.GetAccount();
        var result = await batchService.UpdateBatchAsync(id, request, actor.Id);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    [SwaggerOperation(Summary = "Delete a batch")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteBatch(string id)
    {
        var result = await batchService.DeleteBatchAsync(id);
        return result.ToActionResult();
    }

    /// <summary>
    /// Submit (or resubmit) this batch's payout setup — either linking the
    /// institution's own Paystack account or fresh settlement details of its
    /// own — for platform staff to review. Never takes effect immediately;
    /// see Platform.Api's BatchPayoutsController for the approval step.
    /// </summary>
    [HttpPost("{id}/payout-setup")]
    [Authorize(Roles = "SuperAdmin,ScopedAdmin")]
    [SwaggerOperation(Summary = "Submit batch payout setup for platform approval")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BatchListItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SubmitPayoutSetup(string id, [FromBody] SubmitBatchPayoutSetupRequest request)
    {
        var actor = User.GetAccount();
        var result = await batchService.SubmitPayoutSetupAsync(id, request, actor);
        return result.ToActionResult();
    }

    /// <summary>Real banks or mobile money providers, straight from Paystack — same proxy as Platform.Api's, so a batch's own settlement form never needs to call Platform.Api directly.</summary>
    [HttpGet("payout-setup/banks")]
    [Authorize(Roles = "SuperAdmin,ScopedAdmin")]
    [SwaggerOperation(Summary = "List banks or mobile money providers for batch settlement setup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<BankOption>>))]
    public async Task<IActionResult> GetBanks([FromQuery] string type = "ghipss")
    {
        if (type != "ghipss" && type != "mobile_money")
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("type must be \"ghipss\" (banks) or \"mobile_money\".").ToActionResult();

        var result = await paystackService.ListBanksAsync(type);
        if (!result.Status)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>(result.Message).ToActionResult();

        var banks = result.Data.Select(b => new BankOption(b.Name, b.Code)).ToList();
        return banks.ToOkApiResponse().ToActionResult();
    }

    /// <summary>Looks up the real account holder name for a bank code + account number, straight from the bank via Paystack.</summary>
    [HttpGet("payout-setup/resolve-account")]
    [Authorize(Roles = "SuperAdmin,ScopedAdmin")]
    [SwaggerOperation(Summary = "Resolve an account number to its account holder name")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ResolvedAccountResponse>))]
    public async Task<IActionResult> ResolveAccount([FromQuery] string accountNumber, [FromQuery] string bankCode)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(bankCode))
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("accountNumber and bankCode are both required.").ToActionResult();

        var result = await paystackService.ResolveAccountAsync(accountNumber, bankCode);
        if (!result.Status || result.Data is null)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>(result.Message).ToActionResult();

        return new ResolvedAccountResponse(result.Data.AccountNumber ?? accountNumber, result.Data.AccountName ?? string.Empty)
            .ToOkApiResponse().ToActionResult();
    }
}
