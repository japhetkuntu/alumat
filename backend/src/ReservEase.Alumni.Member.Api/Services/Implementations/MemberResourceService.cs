using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class MemberResourceService(
    IAlumniPgRepository<Resource> resourceRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    ILogger<MemberResourceService> logger) : IMemberResourceService
{
    private async Task<bool> IsApprovedCommunityMemberAsync(string communityId, string memberId)
    {
        var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
        return membership is not null && membership.Status == "Approved";
    }

    private async Task<List<string>> GetApprovedCommunityIdsAsync(string memberId)
    {
        var memberships = await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved");
        return memberships.Select(m => m.CommunityId).ToList();
    }

    private async Task EnrichCommunityNamesAsync(IEnumerable<ResourceDto> results)
    {
        var communityIds = results.Where(d => d.CommunityId != null).Select(d => d.CommunityId!).Distinct().ToList();
        if (communityIds.Count == 0) return;
        var communities = await communityRepo.GetAllAsync(c => communityIds.Contains(c.Id));
        var nameById = communities.ToDictionary(c => c.Id, c => c.Name);
        foreach (var dto in results)
            if (dto.CommunityId != null && nameById.TryGetValue(dto.CommunityId, out var name))
                dto.CommunityName = name;
    }

    public async Task<IApiResponse<PgPagedResult<ResourceDto>>> GetResourcesAsync(ResourceFilter filter, string memberId)
    {
        try
        {
            logger.LogInformation("GetResources request — filter: {Filter}", filter.Serialize());

            if (!string.IsNullOrEmpty(filter.CommunityId) && !await IsApprovedCommunityMemberAsync(filter.CommunityId, memberId))
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<ResourceDto>>("You must be an approved member of this community to view its resources");

            var approvedCommunityIds = string.IsNullOrEmpty(filter.CommunityId) ? await GetApprovedCommunityIdsAsync(memberId) : [];

            var result = await resourceRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => (string.IsNullOrEmpty(filter.CommunityId)
                      ? (r.CommunityId == null || approvedCommunityIds.Contains(r.CommunityId))
                      : r.CommunityId == filter.CommunityId)
                  && (string.IsNullOrEmpty(filter.Category) || r.Category == filter.Category)
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
            await EnrichCommunityNamesAsync(dtoResult.Results);
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
