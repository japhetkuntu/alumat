using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class MentorshipController(IMemberMentorshipService mentorshipService) : DefaultController
{
    [HttpGet("mentors")]
    [SwaggerOperation(Summary = "List mentors", Description = "Get a paginated list of available mentors")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<MentorProfile>>))]
    public async Task<IActionResult> GetMentors([FromQuery] BaseFilter filter)
    {
        var result = await mentorshipService.GetMentorsAsync(filter);
        return result.ToActionResult();
    }

    [HttpPost("mentor-profile")]
    [SwaggerOperation(Summary = "Register as mentor", Description = "Create a mentor profile for the current member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RegisterAsMentor([FromBody] RegisterAsMentorRequest request)
    {
        var member = User.GetAccount();
        var result = await mentorshipService.RegisterAsMentorAsync(request, member);
        return result.ToActionResult();
    }

    [HttpPost("requests")]
    [SwaggerOperation(Summary = "Request mentorship", Description = "Send a mentorship request to a mentor")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RequestMentorship([FromBody] RequestMentorshipRequest request)
    {
        var member = User.GetAccount();
        var result = await mentorshipService.RequestMentorshipAsync(request, member);
        return result.ToActionResult();
    }

    [HttpGet("requests/mine")]
    [SwaggerOperation(Summary = "My mentorship requests", Description = "Get a paginated list of the member's mentorship requests")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<MentorshipRequest>>))]
    public async Task<IActionResult> GetMyRequests([FromQuery] BaseFilter filter)
    {
        var member = User.GetAccount();
        var result = await mentorshipService.GetMyRequestsAsync(member.Id, filter);
        return result.ToActionResult();
    }

    [HttpGet("mentor-profile/mine")]
    [SwaggerOperation(Summary = "My mentor profile", Description = "Get the current member's mentor profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MentorProfile>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetMyMentorProfile()
    {
        var member = User.GetAccount();
        var result = await mentorshipService.GetMyMentorProfileAsync(member.Id);
        return result.ToActionResult();
    }

    [HttpGet("requests/incoming")]
    [SwaggerOperation(Summary = "Incoming requests", Description = "Get mentorship requests sent to the current mentor")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<MentorshipRequest>>))]
    public async Task<IActionResult> GetIncomingRequests([FromQuery] BaseFilter filter)
    {
        var member = User.GetAccount();
        var result = await mentorshipService.GetIncomingRequestsAsync(member.Id, filter);
        return result.ToActionResult();
    }

    [HttpPut("requests/{requestId}/accept")]
    [SwaggerOperation(Summary = "Accept request", Description = "Accept a pending mentorship request")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> AcceptRequest(string requestId)
    {
        var member = User.GetAccount();
        var result = await mentorshipService.AcceptRequestAsync(requestId, member);
        return result.ToActionResult();
    }

    [HttpPut("requests/{requestId}/reject")]
    [SwaggerOperation(Summary = "Reject request", Description = "Reject a pending mentorship request")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RejectRequest(string requestId)
    {
        var member = User.GetAccount();
        var result = await mentorshipService.RejectRequestAsync(requestId, member);
        return result.ToActionResult();
    }
}
