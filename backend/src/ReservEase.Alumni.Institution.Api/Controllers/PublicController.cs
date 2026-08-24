using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Unauthenticated endpoints safe to call before login — used by the
/// Institution Portal frontend to theme itself for the resolved tenant.
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
        if (HttpContext.Items["Institution"] is not InstitutionEntity institution)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this host", Code = 404 });

        var theme = new InstitutionThemeResponse(
            institution.Slug,
            institution.PortalName,
            institution.Name,
            institution.PrimaryColorHex,
            institution.SecondaryColorHex,
            institution.LogoUrl,
            institution.Tagline,
            institution.IconUrl,
            institution.InstitutionPortalTitle,
            institution.InstitutionAuthHeadline,
            institution.InstitutionAuthSubtext,
            institution.DisabledFeatures);

        return Ok(new ApiResponse<InstitutionThemeResponse> { Message = "Success", Code = 200, Data = theme });
    }
}
