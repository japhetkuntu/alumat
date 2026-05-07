using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IMemberResourceService
{
    Task<IApiResponse<PgPagedResult<ResourceDto>>> GetResourcesAsync(ResourceFilter filter);
    Task<IApiResponse<ResourceDto>> GetResourceAsync(string resourceId);
    Task<IApiResponse<object>> IncrementDownloadCountAsync(string resourceId);
}
