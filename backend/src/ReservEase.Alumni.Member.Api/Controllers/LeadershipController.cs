using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Who leads a year group or community, and which titles the people in view hold. Appointments are made by admins; a leader can give up their own role.</summary>
[Authorize]
public class LeadershipController(ILeadershipService leadershipService) : DefaultController
{
    [HttpGet("mine")]
    [SwaggerOperation(Summary = "The roles I hold, and what each one allows")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<MyRoleDto>>))]
    public async Task<IActionResult> GetMine() => (await leadershipService.GetMineAsync(User.GetAccount().Id)).ToActionResult();

    [HttpPost("mine/{postId}/step-down")]
    [SwaggerOperation(Summary = "Give up one of my roles")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> StepDown(string postId) => (await leadershipService.StepDownAsync(User.GetAccount().Id, postId)).ToActionResult();

    [HttpGet]
    [SwaggerOperation(Summary = "Leaders of a group, or the titles held by the given members")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<LeadershipPostDto>>))]
    public async Task<IActionResult> GetPosts([FromQuery] string? scopeType = null, [FromQuery] string? scopeId = null, [FromQuery] string? memberIds = null)
    {
        var ids = memberIds?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (await leadershipService.GetPostsAsync(scopeType, scopeId, ids)).ToActionResult();
    }
}
