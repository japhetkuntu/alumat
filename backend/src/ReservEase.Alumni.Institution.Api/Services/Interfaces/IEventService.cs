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
    Task<IApiResponse<object>> CancelEventAsync(string eventId, AuthData admin);
    Task<IApiResponse<object>> DeleteEventAsync(string eventId, AuthData admin);
    Task<IApiResponse<PgPagedResult<EventRsvpDto>>> GetRsvpsAsync(string eventId, BaseFilter filter, AuthData admin);
    Task<IApiResponse<object>> ReopenRsvpAsync(string eventId, string rsvpId, AuthData admin);
}
