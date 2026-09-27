using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IActivationTrackingService
{
    Task<IApiResponse<ActivationScorecardResponse>> GetScorecardAsync();
    Task<IApiResponse<ActivationScorecardItem>> GetInstitutionAsync(string institutionId);
    Task<IApiResponse<ActivationFunnelResponse>> GetFunnelAsync(int weeks);
    Task<IApiResponse<ActivationScorecardResponse>> UpdateTargetAsync(UpdateActivationTargetRequest request, string actorId, string actorName);
    Task<IApiResponse<ActivationScorecardItem>> UpdateInstitutionSettingsAsync(string institutionId, UpdateInstitutionActivationSettingsRequest request, string actorId, string actorName);
}
