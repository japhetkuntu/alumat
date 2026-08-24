using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberEventService
{
    Task<IApiResponse<PgPagedResult<AlumniEventDto>>> GetEventsAsync(EventFilter filter, string memberId);
    Task<IApiResponse<AlumniEventDto>> GetEventByIdAsync(string eventId);
    Task<IApiResponse<object>> RsvpAsync(RsvpRequest request, AuthData member);
    Task<IApiResponse<object>> CancelRsvpAsync(string eventId, AuthData member);
    Task<IApiResponse<IEnumerable<EventRsvpDto>>> GetMyRsvpsAsync(string memberId, string? status = null);
}
