using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// Manage mentorship profiles and requests.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
public class MentorshipController(IMentorshipService mentorshipService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of mentor profiles.
    /// </summary>
    [HttpGet("profiles")]
    [SwaggerOperation(Summary = "List mentor profiles", Description = "Filter by status (Pending, Active, Paused).")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<MentorProfileDto>>))]
    public async Task<IActionResult> GetProfiles([FromQuery] MentorProfileFilter filter)
    {
        var admin = User.GetAccount();
        var result = await mentorshipService.GetMentorProfilesAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Approve a mentor profile.
    /// </summary>
    [HttpPut("profiles/{profileId}/approve")]
    [SwaggerOperation(Summary = "Approve mentor")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ApproveMentor(string profileId)
    {
        var admin = User.GetAccount();
        var result = await mentorshipService.ApproveMentorAsync(profileId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Reject a mentor profile.
    /// </summary>
    [HttpPut("profiles/{profileId}/reject")]
    [SwaggerOperation(Summary = "Reject mentor")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RejectMentor(string profileId)
    {
        var admin = User.GetAccount();
        var result = await mentorshipService.RejectMentorAsync(profileId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a paginated list of mentorship requests.
    /// </summary>
    [HttpGet("requests")]
    [SwaggerOperation(Summary = "List mentorship requests", Description = "Filter by status (Pending, Accepted, Rejected).")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<MentorshipRequestDto>>))]
    public async Task<IActionResult> GetRequests([FromQuery] MentorshipRequestFilter filter)
    {
        var admin = User.GetAccount();
        var result = await mentorshipService.GetRequestsAsync(filter, admin);
        return result.ToActionResult();
    }
}
