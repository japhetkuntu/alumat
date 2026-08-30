using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Unauthenticated endpoints safe to call before login — used by the
/// public marketing site to let prospective institutions request onboarding.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public class PublicController(IOnboardingLeadService onboardingLeadService) : DefaultController
{
    [HttpPost("onboarding-leads")]
    [SwaggerOperation(Summary = "Submit a request to onboard a new institution")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    public async Task<IActionResult> CreateLead([FromBody] CreateOnboardingLeadRequest request)
    {
        var result = await onboardingLeadService.CreateAsync(request);
        return result.ToActionResult();
    }
}
