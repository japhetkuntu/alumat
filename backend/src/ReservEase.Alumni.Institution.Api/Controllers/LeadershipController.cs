using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Leadership: the roles an institution makes for its leaders (its own names, with the privileges it chooses for each), and who holds
/// which role for which year group or community. Only a full admin may create roles or appoint, because a role is a grant of access.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/leadership")]
public class LeadershipController(ILeadershipService leadershipService) : DefaultController
{
    [HttpGet("privileges")]
    [SwaggerOperation(Summary = "What a role can be allowed to do")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PrivilegeItem>>))]
    public IActionResult GetPrivileges() => leadershipService.GetPrivileges().ToActionResult();

    [HttpGet("settings")]
    [SwaggerOperation(Summary = "How often leaders may message their groups")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LeadershipSettingsItem>))]
    public async Task<IActionResult> GetSettings() => (await leadershipService.GetSettingsAsync()).ToActionResult();

    [HttpPut("settings")]
    [SwaggerOperation(Summary = "Change how often leaders may message their groups")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LeadershipSettingsItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateSettings([FromBody] SaveLeadershipSettingsRequest request)
        => (await leadershipService.UpdateSettingsAsync(request, User.GetAccount())).ToActionResult();

    [HttpGet("roles")]
    [SwaggerOperation(Summary = "The institution's leadership roles")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<LeadershipRoleItem>>))]
    public async Task<IActionResult> GetRoles() => (await leadershipService.GetRolesAsync()).ToActionResult();

    [HttpPost("roles")]
    [SwaggerOperation(Summary = "Create a leadership role")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LeadershipRoleItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateRole([FromBody] SaveLeadershipRoleRequest request)
        => (await leadershipService.CreateRoleAsync(request, User.GetAccount())).ToActionResult();

    [HttpPut("roles/{id}")]
    [SwaggerOperation(Summary = "Rename a role or change what it allows")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LeadershipRoleItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] SaveLeadershipRoleRequest request)
        => (await leadershipService.UpdateRoleAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpDelete("roles/{id}")]
    [SwaggerOperation(Summary = "Delete a role nobody holds")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteRole(string id)
        => (await leadershipService.DeleteRoleAsync(id, User.GetAccount())).ToActionResult();

    [HttpGet]
    [SwaggerOperation(Summary = "Leadership posts for a group, or held by one member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<LeadershipPostItem>>))]
    public async Task<IActionResult> GetPosts([FromQuery] string? scopeType = null, [FromQuery] string? scopeId = null, [FromQuery] string? memberId = null)
        => (await leadershipService.GetPostsAsync(scopeType, scopeId, memberId)).ToActionResult();

    [HttpPost]
    [SwaggerOperation(Summary = "Give a member a role in a group")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LeadershipPostItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Appoint([FromBody] AppointLeaderRequest request)
        => (await leadershipService.AppointAsync(request, User.GetAccount())).ToActionResult();

    [HttpPut("{id}/role")]
    [SwaggerOperation(Summary = "Give a leader a different role in the same place")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LeadershipPostItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeLeaderRoleRequest request)
        => (await leadershipService.ChangeRoleAsync(id, request.RoleId, User.GetAccount())).ToActionResult();

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Remove a member from a role")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Remove(string id)
        => (await leadershipService.RemoveAsync(id, User.GetAccount())).ToActionResult();
}
