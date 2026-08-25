using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Onboard and manage institutions (tenants). Platform staff only.
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/institutions")]
public class InstitutionsController(IInstitutionManagementService institutionService) : DefaultController
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

    [Authorize(Roles = "SuperAdmin,Billing")]
    [HttpPatch("{id}/plan")]
    [SwaggerOperation(Summary = "Update institution plan/limits")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionDetailResponse>))]
    public async Task<IActionResult> UpdatePlan(string id, [FromBody] UpdateInstitutionPlanRequest request)
    {
        var acct = User.GetAccount();
        var result = await institutionService.UpdatePlanAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
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

    /// <summary>How money moved for this institution — gross collected, the platform's cut, and what settled to them.</summary>
    [HttpGet("{id}/revenue")]
    [SwaggerOperation(Summary = "Get institution revenue")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionRevenueResponse>))]
    public async Task<IActionResult> GetRevenue(string id)
    {
        var result = await institutionService.GetRevenueAsync(id);
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
