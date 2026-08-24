using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
[RequireFeature(InstitutionFeatures.Events)]
public class EventsController(IMemberEventService eventService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List events", Description = "Get a paginated list of upcoming alumni events")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<AlumniEventDto>>))]
    public async Task<IActionResult> GetEvents([FromQuery] EventFilter filter)
    {
        var member = User.GetAccount();
        var result = await eventService.GetEventsAsync(filter, member.Id);
        return result.ToActionResult();
    }

    [HttpGet("{eventId}")]
    [SwaggerOperation(Summary = "Get event by ID", Description = "Get full details of a single event")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AlumniEventDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetEvent(string eventId)
    {
        var result = await eventService.GetEventByIdAsync(eventId);
        return result.ToActionResult();
    }

    [HttpPost("rsvp")]
    [SwaggerOperation(Summary = "RSVP", Description = "Register attendance for an event")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Rsvp([FromBody] RsvpRequest request)
    {
        var member = User.GetAccount();
        var result = await eventService.RsvpAsync(request, member);
        return result.ToActionResult();
    }

    [HttpDelete("{eventId}/rsvp")]
    [SwaggerOperation(Summary = "Cancel RSVP", Description = "Cancel attendance registration for an event")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CancelRsvp(string eventId)
    {
        var member = User.GetAccount();
        var result = await eventService.CancelRsvpAsync(eventId, member);
        return result.ToActionResult();
    }

    [HttpGet("my-rsvps")]
    [SwaggerOperation(Summary = "My RSVPs", Description = "Get events the member has RSVP'd to")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IEnumerable<EventRsvpDto>>))]
    public async Task<IActionResult> GetMyRsvps([FromQuery] string? status)
    {
        var member = User.GetAccount();
        var result = await eventService.GetMyRsvpsAsync(member.Id, status);
        return result.ToActionResult();
    }
}
