using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage this institution's communities (sub-groups members can request to
/// join). Institution admins create/edit communities and can promote any
/// approved member to community "Leader" — day-to-day moderation (approving
/// join requests, removing members) is then handled by leaders themselves
/// via the Member Portal, not here.
/// </summary>
[Authorize]
[RequireFeature(InstitutionFeatures.Communities)]
[Route("api/v{version:apiVersion}/communities")]
public class CommunitiesController(ICommunityService communityService) : DefaultController
{
    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin,ScopedAdmin")]
    [SwaggerOperation(Summary = "List communities")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<CommunityListItem>>))]
    public async Task<IActionResult> GetCommunities()
    {
        var result = await communityService.GetCommunitiesAsync();
        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [SwaggerOperation(Summary = "Create a community")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<CommunityListItem>))]
    public async Task<IActionResult> CreateCommunity([FromBody] CreateCommunityRequest request)
    {
        var actor = User.GetAccount();
        var result = await communityService.CreateCommunityAsync(request, actor.Id);
        return result.ToActionResult();
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [SwaggerOperation(Summary = "Update a community")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CommunityListItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateCommunity(string id, [FromBody] UpdateCommunityRequest request)
    {
        var actor = User.GetAccount();
        var result = await communityService.UpdateCommunityAsync(id, request, actor.Id);
        return result.ToActionResult();
    }

    [HttpGet("{id}/members")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [SwaggerOperation(Summary = "List a community's members and pending requests")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<CommunityMemberItem>>))]
    public async Task<IActionResult> GetCommunityMembers(string id)
    {
        var result = await communityService.GetCommunityMembersAsync(id);
        return result.ToActionResult();
    }

    [HttpPut("{id}/members/{memberId}/role")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [SwaggerOperation(Summary = "Promote or demote a community member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SetMemberRole(string id, string memberId, [FromBody] SetCommunityMemberRoleRequest request)
    {
        var actor = User.GetAccount();
        var result = await communityService.SetMemberRoleAsync(id, memberId, request, actor.Id);
        return result.ToActionResult();
    }
}
