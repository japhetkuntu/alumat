using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class MemberNewsService(
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<StaffEntity> adminRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    ILogger<MemberNewsService> logger) : IMemberNewsService
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

    private async Task EnrichCommunityNamesAsync(IEnumerable<NewsPostDto> results)
    {
        var communityIds = results.Where(d => d.CommunityId != null).Select(d => d.CommunityId!).Distinct().ToList();
        if (communityIds.Count == 0) return;
        var communities = await communityRepo.GetAllAsync(c => communityIds.Contains(c.Id));
        var nameById = communities.ToDictionary(c => c.Id, c => c.Name);
        foreach (var dto in results)
            if (dto.CommunityId != null && nameById.TryGetValue(dto.CommunityId, out var name))
                dto.CommunityName = name;
    }

    public async Task<IApiResponse<PgPagedResult<NewsPostDto>>> GetPostsAsync(NewsFilter filter, string memberId)
    {
        try
        {
            logger.LogInformation("GetPosts request — filter: {Filter}", filter.Serialize());

            if (!string.IsNullOrEmpty(filter.CommunityId) && !await IsApprovedCommunityMemberAsync(filter.CommunityId, memberId))
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<NewsPostDto>>("You must be an approved member of this community to view its posts");

            var member = await memberRepo.GetByIdAsync(memberId);
            var memberYear = member?.GraduationYear;

            var approvedCommunityIds = string.IsNullOrEmpty(filter.CommunityId) ? await GetApprovedCommunityIdsAsync(memberId) : [];

            var result = await newsRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "PublishedAt", filter.SortDir ?? "desc",
                p => p.Status == "Published"
                  && (string.IsNullOrEmpty(filter.CommunityId)
                      ? (p.CommunityId == null || approvedCommunityIds.Contains(p.CommunityId))
                      : p.CommunityId == filter.CommunityId)
                  && (p.YearGroups == null || p.YearGroups.Count == 0 || (memberYear.HasValue && p.YearGroups.Contains(memberYear.Value)))
                  && (string.IsNullOrEmpty(filter.Category) || p.Category == filter.Category)
                  && (string.IsNullOrEmpty(filter.Search)
                      || p.Title.Contains(filter.Search)
                      || p.Content.Contains(filter.Search)));

                        await PopulateMissingAuthorsAsync(result.Results);

            var dtoResult = new PgPagedResult<NewsPostDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(p => p.ToDto()).ToList(),
            };
            await EnrichCommunityNamesAsync(dtoResult.Results);
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving news posts — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<NewsPostDto>>("Failed to retrieve posts");
        }
    }

    public async Task<IApiResponse<NewsPostDto>> GetPostByIdAsync(string postId, string memberId)
    {
        try
        {
            logger.LogInformation("GetPostById for postId: {PostId}", postId);

            var post = await newsRepo.GetByIdAsync(postId);
            if (post is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");

            if (post.YearGroups is { Count: > 0 })
            {
                var member = await memberRepo.GetByIdAsync(memberId);
                if (member is null || !post.YearGroups.Contains(member.GraduationYear))
                    return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");
            }

            if (post.Author is null)
            {
                post.Author = await BuildAuthorSnapshotAsync(post.AuthorId);
            }

            return post.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving post {PostId}", postId);
            return ApiResponseExtensions.ToServerErrorApiResponse<NewsPostDto>("Failed to retrieve post");
        }
    }

    private async Task PopulateMissingAuthorsAsync(IEnumerable<NewsPost> posts)
    {
        foreach (var post in posts)
        {
            if (post.Author is not null)
                continue;

            post.Author = await BuildAuthorSnapshotAsync(post.AuthorId);
        }
    }

    private async Task<MemberSnapshot?> BuildAuthorSnapshotAsync(string authorId)
    {
        if (string.IsNullOrWhiteSpace(authorId))
            return null;

        var admin = await adminRepo.GetByIdAsync(authorId);
        if (admin is null)
            return null;

        return new MemberSnapshot
        {
            Id = admin.Id,
            FirstName = admin.FirstName,
            LastName = admin.LastName,
            Email = admin.Email,
            ProfilePictureUrl = null,
        };
    }
}
