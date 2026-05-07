using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Extensions;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class MemberResourceService(
    IAlumniPgRepository<Resource> resourceRepo,
    ILogger<MemberResourceService> logger) : IMemberResourceService
{
    public async Task<IApiResponse<PgPagedResult<ResourceDto>>> GetResourcesAsync(ResourceFilter filter)
    {
        try
        {
            logger.LogInformation("GetResources request — filter: {Filter}", filter.Serialize());
            var result = await resourceRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => (string.IsNullOrEmpty(filter.Category) || r.Category == filter.Category)
                  && (string.IsNullOrEmpty(filter.Type) || r.Type == filter.Type)
                  && (!filter.AddedAfter.HasValue || r.CreatedAt >= filter.AddedAfter.Value)
                  && (!filter.AddedBefore.HasValue || r.CreatedAt <= filter.AddedBefore.Value)
                  && (string.IsNullOrEmpty(filter.Search)
                      || r.Title.Contains(filter.Search)
                      || (r.Description != null && r.Description.Contains(filter.Search))));
            var dtoResult = new PgPagedResult<ResourceDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(r => r.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving resources — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ResourceDto>>("Failed to retrieve resources");
        }
    }

    public async Task<IApiResponse<ResourceDto>> GetResourceAsync(string resourceId)
    {
        try
        {
            logger.LogInformation("GetResource request — resourceId: {ResourceId}", resourceId);
            var resource = await resourceRepo.GetByIdAsync(resourceId);
            if (resource is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ResourceDto>("Resource not found");
            return resource.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving resource — resourceId: {ResourceId}", resourceId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ResourceDto>("Failed to retrieve resource");
        }
    }

    public async Task<IApiResponse<object>> IncrementDownloadCountAsync(string resourceId)
    {
        try
        {
            logger.LogInformation("Increment resource downloads — resourceId: {ResourceId}", resourceId);
            var resource = await resourceRepo.GetByIdAsync(resourceId);
            if (resource is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Resource not found");

            resource.DownloadCount += 1;
            resource.UpdatedAt = DateTime.UtcNow;
            await resourceRepo.UpdateAsync(resource);

            return new object().ToOkApiResponse("Download count incremented");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error incrementing download count — resourceId: {ResourceId}", resourceId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to update resource download count");
        }
    }
}
