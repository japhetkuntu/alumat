using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IAnalyticsService
{
    /// <param name="disabledFeatures">The institution's switched-off features; the parts of the analysis that belong to one are left out.</param>
    Task<IApiResponse<InstitutionAnalyticsDto>> GetAnalyticsAsync(AuthData admin, IReadOnlyCollection<string> disabledFeatures);
}
