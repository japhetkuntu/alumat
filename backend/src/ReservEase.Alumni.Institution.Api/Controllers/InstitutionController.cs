using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;
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
    [Authorize(Roles = "Admin,SuperAdmin")]
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
        institution.HeroImageUrl = request.HeroImageUrl;
        institution.HeroHeadline = request.HeroHeadline;
        institution.UpdatedAt = DateTime.UtcNow;
        await institutionRepo.UpdateAsync(institution);

        return Ok(new ApiResponse<InstitutionResponse> { Message = "Landing content updated", Code = 200, Data = ToDto(institution) });
    }

    /// <summary>How "active member" status is determined — this institution's own operational choice.</summary>
    [Authorize(Roles = "Admin,SuperAdmin")]
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
            i.ContactEmail, i.SupportEmail, i.LogoUrl, i.IconUrl, i.PrimaryColorHex,
            i.InstitutionPortalTitle, i.InstitutionAuthHeadline, i.InstitutionAuthSubtext,
            i.MemberPortalTitle, i.MemberAuthHeadline, i.MemberAuthSubtext,
            i.RequireStudentId, i.MemberActivePolicy, i.DisabledFeatures, i.LandingPageStories, i.NewsBanner,
            i.HeroImageUrl, i.HeroHeadline,
            i.Status, memberPortalUrl);
    }
}
