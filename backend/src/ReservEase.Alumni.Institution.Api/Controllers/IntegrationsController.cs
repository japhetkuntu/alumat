using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>Called by a partner's server with an API key, not by a signed-in user.</summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.IntegrationPolicy)]
public class IntegrationsController(IIntegrationStatusService statusService) : DefaultController
{
    /// <summary>
    /// Is this student ID already a member? If not, the response carries a one-use registration link for that student. A GET, but a
    /// "not found" answer issues a link, so it must never be cached or replayed: the response is marked no-store.
    /// </summary>
    [HttpGet("members/status")]
    [SwaggerOperation(Summary = "Check whether a student ID is already a member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntegrationMemberStatusResponse>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetMemberStatus([FromQuery] string? memberId, [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await statusService.GetMemberStatusAsync(apiKey, memberId);
        return result.ToActionResult();
    }
}
