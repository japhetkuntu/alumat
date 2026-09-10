using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
[RequireFeature(InstitutionFeatures.Contributions)]
public class ContributionsController(IContributionService contributionService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "My contributions", Description = "Get a paginated list of the member's contributions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ContributionDto>>))]
    public async Task<IActionResult> GetMyContributions([FromQuery] ContributionFilter filter)
    {
        var member = User.GetAccount();
        var result = await contributionService.GetMyContributionsAsync(member.Id, filter);
        return result.ToActionResult();
    }

    [HttpGet("membership/status")]
    [SwaggerOperation(Summary = "Membership status", Description = "Get current member's active membership status and expiry")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MembershipStatusResponse>))]
    public async Task<IActionResult> GetMembershipStatus()
    {
        var member = User.GetAccount();
        var result = await contributionService.GetMembershipStatusAsync(member);
        return result.ToActionResult();
    }

    [HttpGet("membership/current-unpaid")]
    [SwaggerOperation(Summary = "Current-year unpaid membership campaigns", Description = "Get current year membership campaigns not yet paid by this member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<CampaignDto>>))]
    public async Task<IActionResult> GetCurrentYearUnpaidMembershipCampaigns()
    {
        var member = User.GetAccount();
        var result = await contributionService.GetCurrentYearUnpaidMembershipCampaignsAsync(member);
        return result.ToActionResult();
    }

    [HttpPost("membership/renew")]
    [SwaggerOperation(Summary = "Renew membership", Description = "Renew membership for 1 or more years using selected membership campaign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RenewMembership([FromBody] InitiateMembershipRenewalRequest request)
    {
        var member = User.GetAccount();
        var result = await contributionService.InitiateMembershipRenewalAsync(request, member);
        return result.ToActionResult();
    }

    [HttpPost("paystack/initiate")]
    [SwaggerOperation(Summary = "Initiate payment", Description = "Start a Paystack payment transaction for a campaign contribution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaystackPaymentRequest request)
    {
        var member = User.GetAccount();
        var result = await contributionService.InitiatePaystackPaymentAsync(request, member);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpPost("paystack/initiate/guest")]
    [SwaggerOperation(Summary = "Initiate payment (guest)", Description = "Start a Paystack payment transaction for a campaign contribution without membership")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> InitiatePaymentGuest([FromBody] InitiatePaystackPaymentRequest request)
    {
        var result = await contributionService.InitiatePaystackPaymentAsync(request, null);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("paystack/verify/{reference}")]
    [SwaggerOperation(Summary = "Verify payment", Description = "Verify a Paystack payment transaction by its reference")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> VerifyPayment(string reference)
    {
        AuthData? member = null;
        if (User?.Identity?.IsAuthenticated == true)
        {
            member = User.GetAccount();
        }
        var result = await contributionService.VerifyPaystackPaymentAsync(reference, member);
        return result.ToActionResult();
    }

    // Anonymous: a guest who just paid via the public campaign page has no
    // session to check status with. Trusted the same way as the Verify
    // endpoint above — by the reference itself, an unguessable Paystack id.
    [AllowAnonymous]
    [HttpGet("paystack/status/{reference}")]
    [SwaggerOperation(Summary = "Get payment status", Description = "Get the current status of a Paystack payment transaction by its reference")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ContributionStatusResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetPaymentStatus(string reference)
    {
        AuthData? member = null;
        if (User?.Identity?.IsAuthenticated == true)
        {
            member = User.GetAccount();
        }
        var result = await contributionService.GetContributionStatusAsync(reference, member);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("paystack/activation-status/{reference}")]
    [SwaggerOperation(Summary = "Get activation payment status (public)", Description = "Read-only status check for the membership activation callback page. Does not trigger any payment processing.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ActivationStatusResponse>))]
    public async Task<IActionResult> GetActivationStatus(string reference)
    {
        var result = await contributionService.GetActivationStatusAsync(reference);
        return result.ToActionResult();
    }

    [HttpPost("proof")]
    [SwaggerOperation(Summary = "Upload proof", Description = "Upload proof of a contribution such as a bank transfer receipt")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ContributionDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UploadProof([FromBody] UploadContributionProofRequest request)
    {
        var member = User.GetAccount();
        var result = await contributionService.UploadProofAsync(request, member);
        return result.ToActionResult();
    }

    [HttpGet("recurring")]
    [SwaggerOperation(Summary = "My recurring gifts", Description = "Get the current member's standing monthly gifts, active and past")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<RecurringContributionDto>>))]
    public async Task<IActionResult> GetMyRecurringGiving()
    {
        var member = User.GetAccount();
        var result = await contributionService.GetMyRecurringGivingAsync(member.Id);
        return result.ToActionResult();
    }

    [HttpPost("recurring/{id}/cancel")]
    [SwaggerOperation(Summary = "Cancel recurring gift", Description = "Stops a standing monthly gift — terminal, a new one must be set up to resume")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CancelRecurringGiving(string id)
    {
        var member = User.GetAccount();
        var result = await contributionService.CancelRecurringGivingAsync(id, member.Id);
        return result.ToActionResult();
    }
}
