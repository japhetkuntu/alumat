using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IEventService
{
    Task<IApiResponse<PgPagedResult<AlumniEventDto>>> GetEventsAsync(EventFilter filter, AuthData admin);
    Task<IApiResponse<AlumniEventDto>> GetEventByIdAsync(string eventId, AuthData admin);
    Task<IApiResponse<AlumniEventDto>> CreateEventAsync(CreateEventRequest request, AuthData admin);
    Task<IApiResponse<AlumniEventDto>> UpdateEventAsync(UpdateEventRequest request, AuthData admin);
    /// <summary>Stop or resume taking new RSVPs and ticket sales without cancelling the event.</summary>
    Task<IApiResponse<AlumniEventDto>> SetRegistrationClosedAsync(string eventId, bool closed, AuthData admin);
    Task<IApiResponse<object>> CancelEventAsync(string eventId, AuthData admin);
    Task<IApiResponse<object>> DeleteEventAsync(string eventId, AuthData admin);
    Task<IApiResponse<PgPagedResult<EventRsvpDto>>> GetRsvpsAsync(string eventId, BaseFilter filter, AuthData admin);
    Task<IApiResponse<EventTicketingDto>> GetTicketingAsync();
    Task<IApiResponse<object>> ReopenRsvpAsync(string eventId, string rsvpId, AuthData admin);
}
