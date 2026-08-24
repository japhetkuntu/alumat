using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberResourceService
{
    Task<IApiResponse<PgPagedResult<ResourceDto>>> GetResourcesAsync(ResourceFilter filter, string memberId);
    Task<IApiResponse<ResourceDto>> GetResourceAsync(string resourceId);
    Task<IApiResponse<object>> IncrementDownloadCountAsync(string resourceId);
}
