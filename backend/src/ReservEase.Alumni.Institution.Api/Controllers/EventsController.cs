using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage alumni events.
/// </summary>
[Authorize(Roles = "SuperAdmin,ScopedAdmin")]
[RequireFeature(InstitutionFeatures.Events)]
public class EventsController(IEventService eventService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of events.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List events")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<AlumniEventDto>>))]
    public async Task<IActionResult> GetEvents([FromQuery] EventFilter filter)
    {
        var admin = User.GetAccount();
        var result = await eventService.GetEventsAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new event.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create event")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<AlumniEventDto>))]
    public async Task<IActionResult> CreateEvent([FromForm] CreateEventRequest request)
    {
        var admin = User.GetAccount();
        var result = await eventService.CreateEventAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Update an existing event.
    /// </summary>
    [HttpPut("{eventId}")]
    [SwaggerOperation(Summary = "Update event")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AlumniEventDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateEvent(string eventId, [FromForm] UpdateEventRequest request)
    {
        request.EventId = eventId;
        var admin = User.GetAccount();
        var result = await eventService.UpdateEventAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Cancel an event.
    /// </summary>
    [HttpPut("{eventId}/cancel")]
    [SwaggerOperation(Summary = "Cancel event")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CancelEvent(string eventId)
    {
        var admin = User.GetAccount();
        var result = await eventService.CancelEventAsync(eventId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete an event.
    /// </summary>
    [HttpDelete("{eventId}")]
    [SwaggerOperation(Summary = "Delete event")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteEvent(string eventId)
    {
        var admin = User.GetAccount();
        var result = await eventService.DeleteEventAsync(eventId, admin);
        return result.ToActionResult();
    }

    /// <summary>Whether this community may charge for tickets, and whether its payout account is ready. Drives the event form.</summary>
    [HttpGet("ticketing")]
    [SwaggerOperation(Summary = "Paid-event availability for this community")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EventTicketingDto>))]
    public async Task<IActionResult> GetTicketing()
    {
        var result = await eventService.GetTicketingAsync();
        return result.ToActionResult();
    }

    /// <summary>Stop or resume taking new RSVPs and ticket sales. Seats already taken stay; the event is not cancelled.</summary>
    [HttpPut("{eventId}/registration")]
    [SwaggerOperation(Summary = "Close or reopen RSVPs")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AlumniEventDto>))]
    public async Task<IActionResult> SetRegistration(string eventId, [FromQuery] bool closed)
    {
        var result = await eventService.SetRegistrationClosedAsync(eventId, closed, User.GetAccount());
        return result.ToActionResult();
    }

    /// <summary>One event, with its ticket sales when it is a paid one.</summary>
    [HttpGet("{eventId}")]
    [SwaggerOperation(Summary = "Get an event")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AlumniEventDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetEvent(string eventId)
    {
        var result = await eventService.GetEventByIdAsync(eventId, User.GetAccount());
        return result.ToActionResult();
    }

    /// <summary>
    /// Get RSVPs for an event.
    /// </summary>
    [HttpGet("{eventId}/rsvps")]
    [SwaggerOperation(Summary = "List event RSVPs")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<EventRsvpDto>>))]
    public async Task<IActionResult> GetRsvps(string eventId, [FromQuery] BaseFilter filter)
    {
        var admin = User.GetAccount();
        var result = await eventService.GetRsvpsAsync(eventId, filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Reopen (re-confirm) a cancelled RSVP.
    /// </summary>
    [HttpPut("{eventId}/rsvps/{rsvpId}/reopen")]
    [SwaggerOperation(Summary = "Reopen RSVP")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ReopenRsvp(string eventId, string rsvpId)
    {
        var admin = User.GetAccount();
        var result = await eventService.ReopenRsvpAsync(eventId, rsvpId, admin);
        return result.ToActionResult();
    }
}

