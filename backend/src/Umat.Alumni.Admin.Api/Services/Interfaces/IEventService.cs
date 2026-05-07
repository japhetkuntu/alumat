using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

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
