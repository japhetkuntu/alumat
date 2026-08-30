using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage the institution's business directory — approve/reject member
/// submissions, approve/reject pending edits to already-approved listings,
/// blacklist/unblacklist listings, add listings directly (auto-approved), and
/// directly edit any listing.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[RequireFeature(InstitutionFeatures.BusinessDirectory)]
[Route("api/v{version:apiVersion}/business-directory")]
public class BusinessDirectoryController(IBusinessDirectoryService businessDirectoryService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List business listings", Description = "Optionally filter by Status — omitted means all statuses.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<BusinessListingDto>>))]
    public async Task<IActionResult> GetListings([FromQuery] BusinessListingFilter filter)
    {
        var result = await businessDirectoryService.GetListingsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get a business listing by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetListing(string id)
    {
        var result = await businessDirectoryService.GetListingByIdAsync(id);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Add a business listing directly", Description = "Admin-authored — auto-approved immediately, no owning member.")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateListing([FromForm] CreateBusinessListingRequest request)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.CreateListingAsync(request, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}/approve")]
    [SwaggerOperation(Summary = "Approve a pending submission")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Approve(string id)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.ApproveListingAsync(id, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}/reject")]
    [SwaggerOperation(Summary = "Reject a pending submission")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Reject(string id, [FromBody] RejectBusinessListingRequest request)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.RejectListingAsync(id, request.AdminNotes, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}/approve-edit")]
    [SwaggerOperation(Summary = "Approve a member's pending edit", Description = "Copies each non-null PendingChanges field onto the live listing, then clears the pending edit.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ApproveEdit(string id)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.ApproveEditAsync(id, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}/reject-edit")]
    [SwaggerOperation(Summary = "Reject a member's pending edit", Description = "Clears the pending edit — the live listing is left untouched.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RejectEdit(string id, [FromBody] RejectBusinessListingRequest request)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.RejectEditAsync(id, request.AdminNotes, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}/blacklist")]
    [SwaggerOperation(Summary = "Blacklist a listing", Description = "Works from any prior status. Locks the owning member out of editing while blacklisted.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Blacklist(string id)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.BlacklistListingAsync(id, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}/unblacklist")]
    [SwaggerOperation(Summary = "Unblacklist a listing", Description = "Restores Status to Approved.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Unblacklist(string id)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.UnblacklistListingAsync(id, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Directly edit a listing", Description = "Admin's own edit — bypasses the pending-edit mechanism and overwrites live fields regardless of current Status.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BusinessListingDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateListing(string id, [FromForm] UpdateBusinessListingRequest request)
    {
        var admin = User.GetAccount();
        var result = await businessDirectoryService.UpdateListingAsync(id, request, admin);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete a business listing", Description = "Hard delete — admin-only cleanup action.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteListing(string id)
    {
        var result = await businessDirectoryService.DeleteListingAsync(id);
        return result.ToActionResult();
    }
}
