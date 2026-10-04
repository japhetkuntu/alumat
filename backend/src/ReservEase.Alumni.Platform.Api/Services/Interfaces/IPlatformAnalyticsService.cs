using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlatformAnalyticsService
{
    /// <param name="includeMoney">False for roles that don't see revenue; the money parts are then left out, not zeroed.</param>
    Task<IApiResponse<PlatformAnalyticsDto>> GetAnalyticsAsync(bool includeMoney);
}
