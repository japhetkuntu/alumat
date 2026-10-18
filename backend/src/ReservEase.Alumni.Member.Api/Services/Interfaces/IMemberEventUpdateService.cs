using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberEventUpdateService
{
    /// <summary>An event's updates for one member: the content if they have a seat, otherwise only how many there are and of what kind.</summary>
    Task<IApiResponse<EventUpdatesResponse>> GetAsync(string eventId, AuthData member);
}
