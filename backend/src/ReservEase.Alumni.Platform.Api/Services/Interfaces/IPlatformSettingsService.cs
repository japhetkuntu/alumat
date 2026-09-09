using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlatformSettingsService
{
    Task<IApiResponse<PlatformSettingsResponse>> GetAsync();
    Task<IApiResponse<PlatformSettingsResponse>> UpdateAsync(UpdatePlatformSettingsRequest request, string updatedBy, string actorName);
}
