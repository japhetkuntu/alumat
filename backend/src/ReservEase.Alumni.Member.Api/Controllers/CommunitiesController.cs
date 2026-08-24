using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// Browse and join communities, and — for members who lead one — moderate
/// its join requests and membership. Every community's existence and basic
/// info is visible to any member; its content (forum, etc.) is not — see
/// each feature's own controller for that enforcement.
/// </summary>
[Authorize]
public class CommunitiesController(ICommunityService communityService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List communities", Description = "All active communities in this institution, with your join status on each")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<CommunityDto>>))]
    public async Task<IActionResult> GetCommunities()
    {
        var member = User.GetAccount();
        var result = await communityService.GetCommunitiesAsync(member.Id);
        return result.ToActionResult();
    }

    [HttpGet("mine")]
    [SwaggerOperation(Summary = "List my communities", Description = "Communities you're an approved member or leader of")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<CommunityDto>>))]
    public async Task<IActionResult> GetMyCommunities()
    {
        var member = User.GetAccount();
        var result = await communityService.GetMyCommunitiesAsync(member.Id);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get a community")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CommunityDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetCommunity(string id)
    {
        var member = User.GetAccount();
        var result = await communityService.GetCommunityAsync(id, member.Id);
        return result.ToActionResult();
    }

    [HttpPost("{id}/join")]
    [SwaggerOperation(Summary = "Request to join a community")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Join(string id)
    {
        var member = User.GetAccount();
        var result = await communityService.JoinAsync(id, member.Id);
        return result.ToActionResult();
    }

    [HttpDelete("{id}/membership")]
    [SwaggerOperation(Summary = "Leave a community (or cancel a pending request)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Leave(string id)
    {
        var member = User.GetAccount();
        var result = await communityService.LeaveAsync(id, member.Id);
        return result.ToActionResult();
    }

    [HttpGet("{id}/members")]
    [SwaggerOperation(Summary = "List a community's approved members")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<CommunityMemberDto>>))]
    public async Task<IActionResult> GetMembers(string id)
    {
        var member = User.GetAccount();
        var result = await communityService.GetMembersAsync(id, member.Id);
        return result.ToActionResult();
    }

    [HttpGet("{id}/join-requests")]
    [SwaggerOperation(Summary = "List pending join requests (leaders only)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<JoinRequestDto>>))]
    public async Task<IActionResult> GetJoinRequests(string id)
    {
        var member = User.GetAccount();
        var result = await communityService.GetJoinRequestsAsync(id, member.Id);
        return result.ToActionResult();
    }

    [HttpPost("{id}/join-requests/{membershipId}/approve")]
    [SwaggerOperation(Summary = "Approve a join request (leaders only)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ApproveJoinRequest(string id, string membershipId)
    {
        var member = User.GetAccount();
        var result = await communityService.DecideJoinRequestAsync(id, membershipId, true, member.Id);
        return result.ToActionResult();
    }

    [HttpPost("{id}/join-requests/{membershipId}/reject")]
    [SwaggerOperation(Summary = "Reject a join request (leaders only)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RejectJoinRequest(string id, string membershipId)
    {
        var member = User.GetAccount();
        var result = await communityService.DecideJoinRequestAsync(id, membershipId, false, member.Id);
        return result.ToActionResult();
    }

    [HttpDelete("{id}/members/{memberId}")]
    [SwaggerOperation(Summary = "Remove a member from the community (leaders only)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RemoveMember(string id, string memberId)
    {
        var member = User.GetAccount();
        var result = await communityService.RemoveMemberAsync(id, memberId, member.Id);
        return result.ToActionResult();
    }
}
