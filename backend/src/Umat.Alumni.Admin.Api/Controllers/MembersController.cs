using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// Manage alumni members — list, approve, reject.
/// </summary>
[Authorize]
public class MembersController(IMemberManagementService memberService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of members with optional filtering.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List members", Description = "Retrieve members with optional status, search, and department filters.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<MemberListItem>>))]
    public async Task<IActionResult> GetMembers([FromQuery] MemberListFilter filter)
    {
        var admin = User.GetAccount();
        var result = await memberService.GetMembersAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a member by ID.
    /// </summary>
    [HttpGet("{memberId}")]
    [SwaggerOperation(Summary = "Get member details")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberDetailItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetMember(string memberId)
    {
        var admin = User.GetAccount();
        var result = await memberService.GetMemberByIdAsync(memberId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Approve a pending member.
    /// </summary>
    [HttpPut("{memberId}/approve")]
    [SwaggerOperation(Summary = "Approve member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ApproveMember(string memberId)
    {
        var admin = User.GetAccount();
        var result = await memberService.ApproveMemberAsync(memberId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Reject a member with an optional reason.
    /// </summary>
    [HttpPut("{memberId}/reject")]
    [SwaggerOperation(Summary = "Reject member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RejectMember(string memberId, [FromBody] RejectMemberRequest request)
    {
        var admin = User.GetAccount();
        var result = await memberService.RejectMemberAsync(memberId, request.Reason, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Ban an active member to prevent portal access.
    /// </summary>
    [HttpPut("{memberId}/ban")]
    [SwaggerOperation(Summary = "Ban member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> BanMember(string memberId, [FromBody] BanMemberRequest request)
    {
        var admin = User.GetAccount();
        var result = await memberService.BanMemberAsync(memberId, request.Reason, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Unban a banned member.
    /// </summary>
    [HttpPut("{memberId}/unban")]
    [SwaggerOperation(Summary = "Unban member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UnbanMember(string memberId)
    {
        var admin = User.GetAccount();
        var result = await memberService.UnbanMemberAsync(memberId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Bulk import pre-existing members with optional pre-paid membership years.
    /// </summary>
    [HttpPost("import")]
    [SwaggerOperation(Summary = "Import members", Description = "Bulk import members, creating them as Active with optional pre-paid memberships.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ImportMembersResult>))]
    public async Task<IActionResult> ImportMembers([FromBody] ImportMembersRequest request)
    {
        var admin = User.GetAccount();
        var result = await memberService.ImportMembersAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Activate membership for specific years (e.g. pre-portal payments).
    /// </summary>
    [HttpPut("{memberId}/activate-membership")]
    [SwaggerOperation(Summary = "Activate membership", Description = "Mark a member's membership as paid for specific campaign years.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ActivateMembership(string memberId, [FromBody] ActivateMembershipRequest request)
    {
        var admin = User.GetAccount();
        var result = await memberService.ActivateMembershipAsync(memberId, request, admin);
        return result.ToActionResult();
    }
}
