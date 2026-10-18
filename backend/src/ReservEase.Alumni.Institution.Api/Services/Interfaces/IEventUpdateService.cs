using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

/// <summary>Posts an organiser adds to an event for the people who are going: any mix of text, photos, video, links and files.</summary>
public interface IEventUpdateService
{
    Task<IApiResponse<List<EventUpdateDto>>> ListAsync(string eventId, AuthData admin);

    /// <param name="files">New uploads, grouped by the id of the block they belong to.</param>
    Task<IApiResponse<EventUpdateDto>> CreateAsync(string eventId, EventUpdateSaveRequest request, IReadOnlyDictionary<string, List<IFormFile>> files, AuthData admin);

    Task<IApiResponse<EventUpdateDto>> UpdateAsync(string eventId, string updateId, EventUpdateSaveRequest request, IReadOnlyDictionary<string, List<IFormFile>> files, AuthData admin);

    Task<IApiResponse<EventUpdateDto>> SetPinnedAsync(string eventId, string updateId, bool pinned, AuthData admin);

    Task<IApiResponse<object>> DeleteAsync(string eventId, string updateId, AuthData admin);
}
