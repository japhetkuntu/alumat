using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IAnnouncementService
{
    Task<IApiResponse<List<AnnouncementResponse>>> GetAnnouncementsAsync();
    Task<IApiResponse<AnnouncementResponse>> SendAsync(SendAnnouncementRequest request, string actorId, string actorName);

    /// <summary>Institution admins matching a name/email search, optionally narrowed to one institution — for the recipient picker.</summary>
    Task<IApiResponse<List<StaffDirectoryEntry>>> SearchStaffAsync(string? search, string? institutionId);
}
