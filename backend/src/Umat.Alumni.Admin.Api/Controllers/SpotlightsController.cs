using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

[Authorize]
public class SpotlightsController(IAdminSpotlightService spotlightService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List spotlights", Description = "Get a paginated list of spotlight submissions, optionally filtered by status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<SpotlightDto>>))]
    public async Task<IActionResult> GetSpotlights(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null)
    {
        var result = await spotlightService.GetSpotlightsAsync(page, pageSize, status);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create spotlight", Description = "Create and feature a spotlight for a member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Create([FromBody] AdminCreateSpotlightRequest body)
    {
        var admin = User.GetAccount();
        var result = await spotlightService.CreateSpotlightAsync(body, admin);
        return result.ToActionResult();
    }

    [HttpPost("{spotlightId}/approve")]
    [SwaggerOperation(Summary = "Approve spotlight", Description = "Approve a spotlight submission to be featured")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Approve(string spotlightId)
    {
        var admin = User.GetAccount();
        var result = await spotlightService.ApproveSpotlightAsync(spotlightId, admin);
        return result.ToActionResult();
    }

    [HttpPost("{spotlightId}/reject")]
    [SwaggerOperation(Summary = "Reject spotlight", Description = "Reject a spotlight submission with an optional reason")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Reject(string spotlightId, [FromBody] RejectSpotlightBody? body)
    {
        var admin = User.GetAccount();
        var result = await spotlightService.RejectSpotlightAsync(spotlightId, body?.Reason, admin);
        return result.ToActionResult();
    }
}

public record RejectSpotlightBody(string? Reason);
