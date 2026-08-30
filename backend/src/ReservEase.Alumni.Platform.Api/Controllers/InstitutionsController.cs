using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Onboard and manage institutions (tenants). Platform staff only.
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/institutions")]
public class InstitutionsController(IInstitutionManagementService institutionService, IPaystackService paystackService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List institutions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<InstitutionListItemResponse>>))]
    public async Task<IActionResult> GetInstitutions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null)
    {
        var result = await institutionService.GetInstitutionsAsync(page, pageSize, search, status);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get institution detail")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> GetInstitution(string id)
    {
        var result = await institutionService.GetInstitutionAsync(id);
        return result.ToActionResult();
    }

    [HttpGet("check-slug")]
    [SwaggerOperation(Summary = "Check slug availability")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SlugAvailabilityResponse>))]
    public async Task<IActionResult> CheckSlug([FromQuery] string slug)
    {
        var result = await institutionService.CheckSlugAsync(slug);
        return result.ToActionResult();
    }

    /// <summary>The two base domains every institution's portals live under — lets the frontend build a live subdomain preview during onboarding without hardcoding a domain itself.</summary>
    [HttpGet("base-domains")]
    [SwaggerOperation(Summary = "Get the platform's member/institution base domains")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BaseDomainsResponse>))]
    public IActionResult GetBaseDomains()
    {
        var result = institutionService.GetBaseDomains();
        return result.ToOkApiResponse().ToActionResult();
    }

    /// <summary>
    /// Real banks or mobile money providers, straight from Paystack — lets
    /// the settlement-details form offer a picklist instead of free-text
    /// bank name/code, which is exactly what CreateSubaccountAsync needs
    /// (the bank code, not the display name) to actually work.
    /// </summary>
    [HttpGet("banks")]
    [SwaggerOperation(Summary = "List banks or mobile money providers for settlement account setup")]
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

    /// <summary>Looks up the real account holder name for a bank code + account number, straight from the bank via Paystack — free for NG/GH, no manual typing of the account name needed.</summary>
    [HttpGet("resolve-account")]
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

    /// <summary>Onboard a new institution: creates the tenant and its first (SuperAdmin) admin.</summary>
    [Authorize(Roles = "SuperAdmin,Sales")]
    [HttpPost]
    [SwaggerOperation(Summary = "Onboard a new institution")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> CreateInstitution([FromBody] CreateInstitutionRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.CreateInstitutionAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin,Billing")]
    [HttpPatch("{id}/status")]
    [SwaggerOperation(Summary = "Update institution status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateInstitutionStatusRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.UpdateStatusAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    /// <summary>How "active member" status is determined for this institution — normally the institution's own choice, but platform staff can set it too (e.g. at onboarding, or a support request).</summary>
    [Authorize(Roles = "SuperAdmin,Support")]
    [HttpPatch("{id}/member-policy")]
    [SwaggerOperation(Summary = "Update the institution's active-member policy")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> UpdateMemberPolicy(string id, [FromBody] UpdateInstitutionMemberPolicyRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.UpdateMemberActivePolicyAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin,Support")]
    [HttpPatch("{id}/branding")]
    [SwaggerOperation(Summary = "Update institution branding")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> UpdateBranding(string id, [FromBody] UpdateInstitutionBrandingRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.UpdateBrandingAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    /// <summary>Enable/disable specific product features for this institution — see InstitutionFeatures for valid keys.</summary>
    [Authorize(Roles = "SuperAdmin,Support")]
    [HttpPatch("{id}/features")]
    [SwaggerOperation(Summary = "Update institution feature flags")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> UpdateFeatures(string id, [FromBody] UpdateInstitutionFeaturesRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.UpdateFeaturesAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    /// <summary>Update this institution's platform fee % and Paystack settlement banking details — keeps the Paystack subaccount in sync.</summary>
    [Authorize(Roles = "SuperAdmin,Billing")]
    [HttpPatch("{id}/payments")]
    [SwaggerOperation(Summary = "Update institution payment settings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> UpdatePayments(string id, [FromBody] UpdateInstitutionPaymentsRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.UpdatePaymentsAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    /// <summary>This institution's own staff accounts — support/ops visibility, not a login credential dump (no password data is ever returned).</summary>
    [HttpGet("{id}/admins")]
    [SwaggerOperation(Summary = "List institution admins")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<InstitutionStaffDto>>))]
    public async Task<IActionResult> GetInstitutionStaff(string id)
    {
        var result = await institutionService.GetInstitutionStaffAsync(id);
        return result.ToActionResult();
    }

    /// <summary>Invite a new staff admin — they receive an email with a link to set their own password, never a temp password relayed by platform staff.</summary>
    [Authorize(Roles = "SuperAdmin,Support")]
    [HttpPost("{id}/admins")]
    [SwaggerOperation(Summary = "Invite institution admin")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<InstitutionStaffDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> InviteInstitutionStaff(string id, [FromBody] InviteInstitutionStaffRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.InviteInstitutionStaffAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim(), acct.Role);
        return result.ToActionResult();
    }

    /// <summary>Disable or re-enable an institution admin's account — a support-facing safety switch, not a hard delete.</summary>
    [Authorize(Roles = "SuperAdmin,Support")]
    [HttpPatch("{id}/admins/{staffId}/disabled")]
    [SwaggerOperation(Summary = "Disable or re-enable an institution admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionStaffDto>))]
    public async Task<IActionResult> SetInstitutionStaffDisabled(string id, string staffId, [FromQuery] bool isDisabled)
    {
        var acct = User.GetAccount();
        var result = await institutionService.SetInstitutionStaffDisabledAsync(id, staffId, isDisabled, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    /// <summary>How money moved for this institution — gross collected, the platform's cut, and what settled to them.</summary>
    [Authorize(Roles = "SuperAdmin,Billing,Support")]
    [HttpGet("{id}/revenue")]
    [SwaggerOperation(Summary = "Get institution revenue")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionRevenueResponse>))]
    public async Task<IActionResult> GetRevenue(string id)
    {
        var result = await institutionService.GetRevenueAsync(id);
        return result.ToActionResult();
    }

    /// <summary>Every payment this institution has collected — Contributions and Store orders, every status — for support staff to troubleshoot a member's payment issue.</summary>
    [Authorize(Roles = "SuperAdmin,Billing,Support")]
    [HttpGet("{id}/payments")]
    [SwaggerOperation(Summary = "Get institution payments (all sources, all statuses)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<PlatformPaymentDto>>))]
    public async Task<IActionResult> GetPayments(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] string? source = null)
    {
        var result = await institutionService.GetPaymentsAsync(id, page, pageSize, status, source);
        return result.ToActionResult();
    }

    /// <summary>Full detail for one payment (Contribution or StoreOrder) — fee breakdown, gateway channel/response, and line items where applicable — for support staff troubleshooting.</summary>
    [Authorize(Roles = "SuperAdmin,Billing,Support")]
    [HttpGet("{institutionId}/payments/{paymentId}")]
    [SwaggerOperation(Summary = "Get a single payment's full detail")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PaymentDetailDto>))]
    public async Task<IActionResult> GetPaymentDetail(string institutionId, string paymentId, [FromQuery] string? source = null)
    {
        var result = await institutionService.GetPaymentDetailAsync(institutionId, paymentId, source);
        return result.ToActionResult();
    }

    /// <summary>Update this institution's Member Portal landing page Stories and news banner — institution admins can also edit this themselves (Institution.Api).</summary>
    [Authorize(Roles = "SuperAdmin,Support")]
    [HttpPatch("{id}/landing-content")]
    [SwaggerOperation(Summary = "Update institution landing page content")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> UpdateLandingContent(string id, [FromBody] UpdateInstitutionLandingContentRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.UpdateLandingContentAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
