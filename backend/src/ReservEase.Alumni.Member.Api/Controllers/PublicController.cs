using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// Unauthenticated endpoints safe to call before login — used by the
/// Member Portal frontend to theme itself for the resolved tenant, and by
/// the public marketing site (also served from this app — see
/// platform-marketing-page.tsx) for the institution-onboarding form.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
public class PublicController(IAlumniPgRepository<OnboardingLead> onboardingLeadRepo) : DefaultController
{
    /// <summary>
    /// Branding for the institution resolved from the request's Host header
    /// (see TenantResolutionMiddleware). Falls back to the configured default
    /// tenant when no real subdomain/custom-domain match is found.
    /// </summary>
    [HttpGet("institution/theme")]
    [SwaggerOperation(Summary = "Get the current tenant's branding/theme")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionThemeResponse>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public IActionResult GetInstitutionTheme()
    {
        if (HttpContext.Items["Institution"] is not Institution institution)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this host", Code = 404 });

        var theme = new InstitutionThemeResponse(
            institution.Slug,
            institution.PortalName,
            string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName,
            institution.PrimaryColorHex,
            institution.SecondaryColorHex,
            institution.LogoUrl,
            institution.Tagline,
            institution.IconUrl,
            institution.MemberPortalTitle,
            institution.MemberAuthHeadline,
            institution.MemberAuthSubtext,
            institution.RequireStudentId,
            institution.DisabledFeatures,
            institution.LandingPageStories,
            institution.NewsBanner,
            institution.HeroImageUrls,
            institution.HeroHeadline);

        return Ok(new ApiResponse<InstitutionThemeResponse> { Message = "Success", Code = 200, Data = theme });
    }

    /// <summary>
    /// A prospective institution's request to be onboarded, from the public
    /// marketing site. Spam-worthy like login, so this overrides the class's
    /// read-only rate limit with the stricter auth policy.
    /// </summary>
    [HttpPost("onboarding-leads")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Submit a request to onboard a new institution")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    public async Task<IActionResult> CreateOnboardingLead([FromBody] CreateOnboardingLeadRequest request)
    {
        var lead = new OnboardingLead
        {
            InstitutionName = request.InstitutionName,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Country = request.Country,
            EstimatedMemberCount = request.EstimatedMemberCount,
            Message = request.Message,
        };
        await onboardingLeadRepo.AddAsync(lead);

        var response = new OnboardingLeadResponse(lead.Id, lead.InstitutionName, lead.ContactEmail, lead.Status);
        return Created(string.Empty, new ApiResponse<OnboardingLeadResponse> { Message = "Created", Code = 201, Data = response });
    }
}
