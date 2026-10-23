using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IInstitutionIntegrationService
{
    Task<IApiResponse<IntegrationSettingsResponse>> GetAsync(string institutionId);
    Task<IApiResponse<IntegrationSettingsResponse>> UpdateAsync(string institutionId, UpdateIntegrationRequest request, string actorId, string actorName);
    Task<IApiResponse<CreatedIntegrationKeyResponse>> CreateKeyAsync(string institutionId, CreateIntegrationKeyRequest request, string actorId, string actorName);
    Task<IApiResponse<object>> RevokeKeyAsync(string institutionId, string keyId, string actorId, string actorName);
}
