using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>The platform team and who reports to whom. A SuperAdmin sees everyone; a representative sees their own branch.</summary>
[Authorize(Roles = "SuperAdmin,Representative")]
public class TeamController(ITeamService team) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "The team structure with how each person's work is going")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TeamViewDto>))]
    public async Task<IActionResult> Get() => (await team.GetAsync()).ToActionResult();

    [HttpGet("assignees")]
    [SwaggerOperation(Summary = "People work can be handed to")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<TeamAssigneeDto>>))]
    public async Task<IActionResult> Assignees() => (await team.GetAssigneesAsync()).ToActionResult();

    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{staffId}/manager")]
    [SwaggerOperation(Summary = "Change who someone reports to")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TeamMemberDto>))]
    public async Task<IActionResult> SetManager(string staffId, [FromBody] SetManagerRequest request)
    {
        var acct = User.GetAccount();
        return (await team.SetManagerAsync(staffId, request.ManagerId, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }

    [HttpPost("reassign")]
    [SwaggerOperation(Summary = "Hand institutions and leads to someone else", Description = "Only what the caller can see, and only to someone in the caller's own team.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ReassignResult>))]
    public async Task<IActionResult> Reassign([FromBody] ReassignRequest request)
    {
        var acct = User.GetAccount();
        return (await team.ReassignAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }
}
