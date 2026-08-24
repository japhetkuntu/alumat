using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// Unauthenticated endpoints safe to call before login — used by the
/// Member Portal frontend to theme itself for the resolved tenant.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
public class PublicController : DefaultController
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
            institution.Name,
            institution.PrimaryColorHex,
            institution.LogoUrl,
            institution.Tagline,
            institution.IconUrl,
            institution.MemberPortalTitle,
            institution.MemberAuthHeadline,
            institution.MemberAuthSubtext,
            institution.RequireStudentId,
            institution.DisabledFeatures,
            institution.LandingPageStories,
            institution.NewsBanner);

        return Ok(new ApiResponse<InstitutionThemeResponse> { Message = "Success", Code = 200, Data = theme });
    }
}
