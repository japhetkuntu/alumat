using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Institution.Api.Controllers;

[Authorize(Roles = "SuperAdmin")]
[RequireFeature(InstitutionFeatures.Spotlights)]
public class SpotlightsController(IInstitutionSpotlightService spotlightService) : DefaultController
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

    [HttpPut("{spotlightId}")]
    [SwaggerOperation(Summary = "Update spotlight", Description = "Update an already-added spotlight's title, story, or image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Update(string spotlightId, [FromBody] UpdateSpotlightRequest body)
    {
        var admin = User.GetAccount();
        var result = await spotlightService.UpdateSpotlightAsync(spotlightId, body, admin);
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

    [HttpPost("{spotlightId}/archive")]
    [SwaggerOperation(Summary = "Archive spotlight", Description = "Hide an old/unwanted spotlight from the admin list and the public site, without deleting it")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Archive(string spotlightId)
    {
        var admin = User.GetAccount();
        var result = await spotlightService.ArchiveSpotlightAsync(spotlightId, admin);
        return result.ToActionResult();
    }

    [HttpPost("{spotlightId}/feature")]
    [SwaggerOperation(Summary = "Feature spotlight", Description = "Pick this approved spotlight to show on the public landing page, replacing whichever one was featured before")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Feature(string spotlightId)
    {
        var admin = User.GetAccount();
        var result = await spotlightService.SetFeaturedAsync(spotlightId, admin);
        return result.ToActionResult();
    }

    [HttpPost("{spotlightId}/unfeature")]
    [SwaggerOperation(Summary = "Unfeature spotlight", Description = "Stop showing this spotlight on the public landing page — falls back to normal ordering")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Unfeature(string spotlightId)
    {
        var admin = User.GetAccount();
        var result = await spotlightService.UnfeatureSpotlightAsync(spotlightId, admin);
        return result.ToActionResult();
    }
}

public record RejectSpotlightBody(string? Reason);
