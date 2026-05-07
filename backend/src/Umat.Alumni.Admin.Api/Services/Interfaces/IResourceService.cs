using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IResourceService
{
    Task<IApiResponse<PgPagedResult<ResourceDto>>> GetResourcesAsync(ResourceFilter filter, AuthData admin);
    Task<IApiResponse<ResourceDto>> GetResourceAsync(string resourceId, AuthData admin);
    Task<IApiResponse<ResourceDto>> CreateResourceAsync(CreateResourceRequest request, AuthData admin);
    Task<IApiResponse<ResourceDto>> UpdateResourceAsync(UpdateResourceRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteResourceAsync(string resourceId, AuthData admin);
}
