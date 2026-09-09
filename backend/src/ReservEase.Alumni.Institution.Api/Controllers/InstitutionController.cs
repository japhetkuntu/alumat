using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Self-service view of the current institution's profile and branding.
/// Scoped to the current request's resolved tenant
/// (<see cref="ICurrentTenantService"/>) — staff can never target another
/// institution's record; there is no institution id in the route.
///
/// Institution staff can no longer edit branding themselves — that's now
/// platform-staff-only (Platform.Api's InstitutionsController), so most of
/// this stays display-only. The one deliberate carve-out is the Member
/// Portal landing page's Stories and news banner (<see cref="UpdateLandingContent"/>),
/// which institution admins are meant to keep current themselves.
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/institution")]
public class InstitutionController(
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant,
    IConfiguration config) : DefaultController
{
    /// <summary>Get the current institution's profile and branding (mostly read-only).</summary>
    [HttpGet("me")]
    [SwaggerOperation(Summary = "Get current institution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionResponse>))]
    public async Task<IActionResult> GetCurrentInstitution()
    {
        var institution = await GetResolvedInstitutionAsync();
        if (institution is null)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this request", Code = 404 });

        return Ok(new ApiResponse<InstitutionResponse> { Message = "Success", Code = 200, Data = ToDto(institution) });
    }

    /// <summary>Update this institution's Member Portal landing page Stories and news banner — the one piece of content institution admins may edit themselves.</summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPatch("me/landing-content")]
    [SwaggerOperation(Summary = "Update landing page stories and news banner")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionResponse>))]
    public async Task<IActionResult> UpdateLandingContent([FromBody] UpdateLandingContentRequest request)
    {
        var institution = await GetResolvedInstitutionAsync();
        if (institution is null)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this request", Code = 404 });

        institution.LandingPageStories = request.LandingPageStories;
        institution.NewsBanner = request.NewsBanner;
        institution.HeroImageUrls = request.HeroImageUrls;
        institution.HeroHeadline = request.HeroHeadline;
        institution.UpdatedAt = DateTime.UtcNow;
        await institutionRepo.UpdateAsync(institution);

        return Ok(new ApiResponse<InstitutionResponse> { Message = "Landing content updated", Code = 200, Data = ToDto(institution) });
    }

    /// <summary>How "active member" status is determined — this institution's own operational choice.</summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPatch("me/member-policy")]
    [SwaggerOperation(Summary = "Update the active-member policy")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMemberActivePolicy([FromBody] UpdateMemberActivePolicyRequest request)
    {
        if (request.MemberActivePolicy != MembershipActivityCalculator.DuesRequiredPolicy
            && request.MemberActivePolicy != MembershipActivityCalculator.ApprovedOnlyPolicy)
        {
            return BadRequest(new ApiResponse<object> { Message = "MemberActivePolicy must be either \"DuesRequired\" or \"ApprovedOnly\"", Code = 400 });
        }

        var institution = await GetResolvedInstitutionAsync();
        if (institution is null)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this request", Code = 404 });

        institution.MemberActivePolicy = request.MemberActivePolicy;
        institution.UpdatedAt = DateTime.UtcNow;
        await institutionRepo.UpdateAsync(institution);

        return Ok(new ApiResponse<InstitutionResponse> { Message = "Active-member policy updated", Code = 200, Data = ToDto(institution) });
    }

    /// <summary>
    /// Toggles the self-service Digest/RecurringGiving features (see
    /// InstitutionFeatures.SelfService — everything else in DisabledFeatures
    /// is left untouched, since every other key stays platform-staff-only),
    /// plus the signup membership-activation prompt, which isn't a
    /// DisabledFeatures key at all since it's opt-in (default off) rather
    /// than opt-out.
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPatch("me/self-service-features")]
    [SwaggerOperation(Summary = "Enable or disable the digest and recurring-giving features, and the signup membership-activation prompt")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionResponse>))]
    public async Task<IActionResult> UpdateSelfServiceFeatures([FromBody] UpdateSelfServiceFeaturesRequest request)
    {
        var institution = await GetResolvedInstitutionAsync();
        if (institution is null)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this request", Code = 404 });

        var disabled = new HashSet<string>(institution.DisabledFeatures);
        void Set(string feature, bool enabled)
        {
            if (enabled) disabled.Remove(feature);
            else disabled.Add(feature);
        }
        Set(InstitutionFeatures.Digest, request.DigestEnabled);
        Set(InstitutionFeatures.RecurringGiving, request.RecurringGivingEnabled);

        institution.DisabledFeatures = disabled.ToList();
        institution.PromptMembershipActivationAtSignup = request.PromptMembershipActivationAtSignup;
        institution.UpdatedAt = DateTime.UtcNow;
        await institutionRepo.UpdateAsync(institution);

        return Ok(new ApiResponse<InstitutionResponse> { Message = "Features updated", Code = 200, Data = ToDto(institution) });
    }

    /// <summary>
    /// Submit (or resubmit) this institution's own settlement details for
    /// platform staff to review — never takes effect immediately, mirroring
    /// Batch payout setup exactly (see Institution.Api's
    /// BatchesController.SubmitPayoutSetup / Platform.Api's
    /// InstitutionPayoutsController for the approval step). SuperAdmin only —
    /// unlike a batch's own payout setup, a ScopedAdmin never touches the
    /// institution's own payment info.
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("me/payout-setup")]
    [SwaggerOperation(Summary = "Submit institution payout setup for platform approval")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SubmitPayoutSetup([FromBody] SubmitInstitutionPayoutSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SettlementBankCode) || string.IsNullOrWhiteSpace(request.SettlementAccountNumber))
            return BadRequest(new ApiResponse<object> { Message = "Bank and account number are required", Code = 400 });

        var institution = await GetResolvedInstitutionAsync();
        if (institution is null)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this request", Code = 404 });

        institution.PendingPayoutChanges = new InstitutionPayoutPendingChanges
        {
            SettlementBankCode = request.SettlementBankCode,
            SettlementBankName = request.SettlementBankName,
            SettlementAccountNumber = request.SettlementAccountNumber,
            SettlementAccountName = request.SettlementAccountName,
        };
        institution.PayoutStatus = "Pending";
        institution.UpdatedAt = DateTime.UtcNow;
        await institutionRepo.UpdateAsync(institution);

        return Ok(new ApiResponse<InstitutionResponse> { Message = "Payout setup submitted for platform review", Code = 200, Data = ToDto(institution) });
    }

    private async Task<InstitutionEntity?> GetResolvedInstitutionAsync() =>
        string.IsNullOrEmpty(currentTenant.InstitutionId)
            ? null
            : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);

    /// <summary>
    /// The Member Portal lives on a completely different base domain from this
    /// one (e.g. this API's own PlatformBaseDomain is "admin.alumunion.com",
    /// the member one is bare "alumunion.com") — there's no way to derive one
    /// from the other by string manipulation without baking in a naming
    /// convention, so it's configured explicitly, purely for building this
    /// shareable link. Never used for tenant resolution.
    /// </summary>
    private InstitutionResponse ToDto(InstitutionEntity i)
    {
        var memberBaseDomain = config["MemberPortalBaseDomain"];
        var memberPortalUrl = string.IsNullOrWhiteSpace(memberBaseDomain)
            ? null
            : $"https://{i.Slug}.{memberBaseDomain}";

        return new(
            i.Id, i.Name, i.Slug, i.CustomDomain, i.PortalName, i.Tagline,
            i.ContactEmail, i.SupportEmail, i.LogoUrl, i.IconUrl, i.PrimaryColorHex, i.SecondaryColorHex,
            i.InstitutionPortalTitle, i.InstitutionAuthHeadline, i.InstitutionAuthSubtext,
            i.MemberPortalTitle, i.MemberAuthHeadline, i.MemberAuthSubtext,
            i.RequireStudentId, i.MemberActivePolicy, i.PromptMembershipActivationAtSignup, i.DisabledFeatures, i.LandingPageStories, i.NewsBanner,
            i.HeroImageUrls, i.HeroHeadline,
            i.Status, memberPortalUrl,
            i.PayoutStatus, i.SettlementBankName, i.SettlementAccountNumber, i.SettlementAccountName);
    }
}
