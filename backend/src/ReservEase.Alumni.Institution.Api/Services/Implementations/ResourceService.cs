using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class ResourceService(
    IAlumniPgRepository<Resource> resourceRepo,
    IStorageService storageService,
    ILogger<ResourceService> logger) : IResourceService
{
    public async Task<IApiResponse<PgPagedResult<ResourceDto>>> GetResourcesAsync(ResourceFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetResources request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
            var communityIds = admin.CommunityIds ?? new List<string>();
            var result = await resourceRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => (string.IsNullOrEmpty(filter.Category) || r.Category == filter.Category)
                  && (string.IsNullOrEmpty(filter.Type) || r.Type == filter.Type)
                  && (!filter.AddedAfter.HasValue || r.CreatedAt >= filter.AddedAfter.Value)
                  && (!filter.AddedBefore.HasValue || r.CreatedAt <= filter.AddedBefore.Value)
                  && (string.IsNullOrEmpty(filter.Search)
                      || r.Title.Contains(filter.Search)
                      || (r.Description != null && r.Description.Contains(filter.Search)))
                  && (isSuper
                      || (r.YearGroups != null && r.YearGroups.Any(__y => yearGroups.Contains(__y)))
                      || (r.CommunityId != null && communityIds.Contains(r.CommunityId))));
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

    public async Task<IApiResponse<ResourceDto>> GetResourceAsync(string resourceId, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetResource request — resourceId: {ResourceId} (admin: {AdminId})", resourceId, admin.Id);
            var resource = await resourceRepo.GetByIdAsync(resourceId);
            if (resource is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ResourceDto>("Resource not found");

            if (!admin.CanViewScopedItem(resource.YearGroups, resource.CommunityId))
            {
                logger.LogWarning("Denied resource view access for admin {AdminId} to resource {ResourceId} (adminYear={AdminYear}, resourceYears={ResourceYears})",
                    admin.Id, resourceId, admin.GraduationYear, resource.YearGroups ?? new List<int>());
                return ApiResponseExtensions.ToNotFoundApiResponse<ResourceDto>("Resource not found");
            }

            return resource.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving resource — resourceId: {ResourceId}", resourceId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ResourceDto>("Failed to retrieve resource");
        }
    }

    public async Task<IApiResponse<ResourceDto>> CreateResourceAsync(CreateResourceRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreateResource request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            var resource = new Resource
            {
                CommunityId = request.CommunityId,
                Title = request.Title,
                Description = request.Description,
                Category = request.Category,
                Type = request.Type,
                ExternalUrl = request.ExternalUrl,
                UploadedBy = admin.Id,
                CreatedBy = admin.Id,
                YearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups),
            };

            if (request.File is not null)
            {
                var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(request.File.FileName)}";
                resource.FileUrl = await storageService.UploadFileAsync(request.File, fileName);
            }

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                resource.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            await resourceRepo.AddAsync(resource);
            logger.LogInformation("Resource {ResourceId} created by admin {AdminId}", resource.Id, admin.Id);
            return resource.ToDto().ToCreatedApiResponse("Resource created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating resource: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ResourceDto>("Failed to create resource");
        }
    }

    public async Task<IApiResponse<ResourceDto>> UpdateResourceAsync(UpdateResourceRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdateResource request for resourceId: {ResourceId} by admin {AdminId}", request.ResourceId, admin.Id);
            var resource = await resourceRepo.GetByIdAsync(request.ResourceId);
            if (resource is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ResourceDto>("Resource not found");

            if (!admin.CanModifyScopedItem(resource.YearGroups, resource.CreatedBy, resource.CommunityId))
            {
                logger.LogWarning("Denied resource update access for admin {AdminId} to resource {ResourceId} (adminYear={AdminYear}, resourceYears={ResourceYears}, createdBy={CreatedBy})",
                    admin.Id, request.ResourceId, admin.GraduationYear, resource.YearGroups ?? new List<int>(), resource.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<ResourceDto>("Resource not found");
            }

            resource.CommunityId = request.CommunityId;
            resource.Title = request.Title;
            resource.Description = request.Description;
            resource.Category = request.Category;
            resource.Type = request.Type;
            resource.ExternalUrl = request.ExternalUrl;
            resource.YearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);

            if (request.File is not null)
            {
                var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(request.File.FileName)}";
                resource.FileUrl = await storageService.UploadFileAsync(request.File, fileName);
            }

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                resource.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            resource.UpdatedAt = DateTime.UtcNow;
            resource.UpdatedBy = admin.Id;
            await resourceRepo.UpdateAsync(resource);
            logger.LogInformation("Resource {ResourceId} updated by admin {AdminId}", resource.Id, admin.Id);
            return resource.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating resource {ResourceId} by admin {AdminId}", request.ResourceId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ResourceDto>("Failed to update resource");
        }
    }

    public async Task<IApiResponse<object>> DeleteResourceAsync(string resourceId, AuthData admin)
    {
        try
        {
            logger.LogInformation("DeleteResource request for resourceId: {ResourceId} (admin: {AdminId})", resourceId, admin.Id);

            var resource = await resourceRepo.GetByIdAsync(resourceId);
            if (resource is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Resource not found");

            if (!admin.CanModifyScopedItem(resource.YearGroups, resource.CreatedBy, resource.CommunityId))
            {
                logger.LogWarning("Denied resource delete access for admin {AdminId} to resource {ResourceId} (adminYear={AdminYear}, resourceYears={ResourceYears}, createdBy={CreatedBy})",
                    admin.Id, resourceId, admin.GraduationYear, resource.YearGroups ?? new List<int>(), resource.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Resource not found");
            }

            await resourceRepo.RemoveAsync(resource);
            logger.LogInformation("Resource {ResourceId} deleted", resourceId);
            return new object().ToOkApiResponse("Resource deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting resource {ResourceId}", resourceId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete resource");
        }
    }
}
