using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IResourceService
{
    Task<IApiResponse<PgPagedResult<ResourceDto>>> GetResourcesAsync(ResourceFilter filter, AuthData admin);
    Task<IApiResponse<ResourceDto>> GetResourceAsync(string resourceId, AuthData admin);
    Task<IApiResponse<ResourceDto>> CreateResourceAsync(CreateResourceRequest request, AuthData admin);
    Task<IApiResponse<ResourceDto>> UpdateResourceAsync(UpdateResourceRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteResourceAsync(string resourceId, AuthData admin);
}
