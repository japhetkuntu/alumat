using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// Browse the public business directory, and manage the caller's own
/// listing — submit for approval, edit (edits to an approved listing become a
/// pending edit awaiting admin approval), hide/unhide, withdraw while still
/// pending.
/// </summary>
[Authorize]
[RequireFeature(InstitutionFeatures.BusinessDirectory)]
[Route("api/v{version:apiVersion}/business-directory")]
public class BusinessDirectoryController(IBusinessDirectoryService businessDirectoryService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Browse approved, visible business listings", Description = "Optional Search filters by business name.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<BusinessListingDto>>))]
    public async Task<IActionResult> GetListings([FromQuery] BusinessListingFilter filter)
    {
        var result = await businessDirectoryService.GetListingsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("mine")]
    [SwaggerOperation(Summary = "Get my own business listing(s)", Description = "Regardless of status/hidden-state, so the owner can see pending/blacklisted/pending-edit state.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<BusinessListingDto>>))]
    public async Task<IActionResult> GetMyListings()
    {
        var member = User.GetAccount();
        var result = await businessDirectoryService.GetMyListingsAsync(member.Id);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get a business listing by ID", Description = "Same visibility filter as the browse list — 404 if the listing exists but isn't approved and visible.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetListing(string id)
    {
        var result = await businessDirectoryService.GetListingByIdAsync(id);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Submit a new business listing for review", Description = "A member may have at most one listing — returns 409 if one already exists.")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SubmitListing([FromForm] SubmitBusinessListingRequest request)
    {
        var member = User.GetAccount();
        var result = await businessDirectoryService.SubmitListingAsync(request, member);
        return result.ToActionResult();
    }

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Edit my business listing", Description = "If the listing is Approved, this creates a pending edit awaiting admin approval instead of going live immediately. Blocked while Blacklisted.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateMyListing(string id, [FromForm] UpdateMyBusinessListingRequest request)
    {
        var member = User.GetAccount();
        var result = await businessDirectoryService.UpdateMyListingAsync(id, request, member);
        return result.ToActionResult();
    }

    [HttpPut("{id}/hide")]
    [SwaggerOperation(Summary = "Hide my listing from the public directory", Description = "No approval needed — independent of the approval workflow. Works even while Blacklisted.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> HideListing(string id)
    {
        var member = User.GetAccount();
        var result = await businessDirectoryService.HideListingAsync(id, member);
        return result.ToActionResult();
    }

    [HttpPut("{id}/unhide")]
    [SwaggerOperation(Summary = "Unhide my listing", Description = "No approval needed.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UnhideListing(string id)
    {
        var member = User.GetAccount();
        var result = await businessDirectoryService.UnhideListingAsync(id, member);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Withdraw my pending submission", Description = "Only allowed while Status is Pending.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteMyListing(string id)
    {
        var member = User.GetAccount();
        var result = await businessDirectoryService.DeleteMyListingAsync(id, member);
        return result.ToActionResult();
    }
}
