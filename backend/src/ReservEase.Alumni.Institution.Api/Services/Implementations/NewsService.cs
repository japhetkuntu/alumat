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
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class NewsService(
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<StaffEntity> adminRepo,
    IStorageService storageService,
    ICurrentTenantService currentTenant,
    ILogger<NewsService> logger) : INewsService
{
    public async Task<IApiResponse<PgPagedResult<NewsPostDto>>> GetPostsAsync(NewsFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetPosts request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
            var communityIds = admin.CommunityIds ?? new List<string>();

            var result = await newsRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                p => (string.IsNullOrEmpty(filter.Status) || p.Status == filter.Status)
                  && (string.IsNullOrEmpty(filter.Search)
                      || p.Title.Contains(filter.Search)
                      || p.Content.Contains(filter.Search))
                  && (isSuper || p.CreatedBy == admin.Id
                      || (p.YearGroups != null && p.YearGroups.Any(__y => yearGroups.Contains(__y)))
                      || (p.CommunityId != null && communityIds.Contains(p.CommunityId))));

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
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving news posts — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<NewsPostDto>>("Failed to retrieve posts");
        }
    }

    public async Task<IApiResponse<NewsPostDto>> GetPostAsync(string postId, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetPost request — postId: {PostId} (admin: {AdminId})", postId, admin.Id);
            var post = await newsRepo.GetByIdAsync(postId);
            if (post is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");

            if (!admin.CanViewScopedItem(post.YearGroups, post.CommunityId))
            {
                logger.LogWarning("Denied news post view access for admin {AdminId} to post {PostId} (adminYear={AdminYear}, postYears={PostYears})",
                    admin.Id, postId, admin.GraduationYear, post.YearGroups ?? new List<int>());
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
            logger.LogError(e, "Error retrieving news post — postId: {PostId}", postId);
            return ApiResponseExtensions.ToServerErrorApiResponse<NewsPostDto>("Failed to retrieve post");
        }
    }

    public async Task<IApiResponse<NewsPostDto>> CreatePostAsync(CreateNewsPostRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreatePost request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            var resolvedCommunityId = admin.ResolveCommunityForCreation(request.CommunityId);
            var resolvedYearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            ScopeAuthorizationExtensions.NormalizeAudience(ref resolvedYearGroups, ref resolvedCommunityId);

            var post = new NewsPost
            {
                CommunityId = resolvedCommunityId,
                Title = request.Title,
                Content = request.Content,
                Category = request.Category,
                IsPinned = request.IsPinned,
                Status = request.Status ?? "Draft",
                PublishedAt = request.Status == "Published" ? DateTime.UtcNow : null,
                AuthorId = admin.Id,
                Author = new MemberSnapshot
                {
                    Id = admin.Id,
                    FirstName = admin.FirstName,
                    LastName = admin.LastName,
                    Email = admin.Email,
                    ProfilePictureUrl = null,
                },
                YearGroups = resolvedYearGroups,
                YoutubeVideoUrls = request.YoutubeVideoUrls,
                CreatedBy = admin.Id,
            };

            if (request.Images is { Count: > 0 })
                post.ImageUrls = await storageService.BulkUploadFilesAsync(request.Images, institutionSlug: currentTenant.InstitutionSlug ?? "");

            await newsRepo.AddAsync(post);
            logger.LogInformation("Post {PostId} created by admin {AdminId}", post.Id, admin.Id);
            return post.ToDto().ToCreatedApiResponse("Post created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating news post: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<NewsPostDto>("Failed to create post");
        }
    }

    public async Task<IApiResponse<NewsPostDto>> UpdatePostAsync(UpdateNewsPostRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdatePost request for postId: {PostId} by admin {AdminId}", request.PostId, admin.Id);
            var post = await newsRepo.GetByIdAsync(request.PostId);
            if (post is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");

            if (!admin.CanModifyScopedItem(post.YearGroups, post.CreatedBy, post.CommunityId))
            {
                logger.LogWarning("Denied news post update access for admin {AdminId} to post {PostId} (adminYear={AdminYear}, postYears={PostYears}, createdBy={CreatedBy})",
                    admin.Id, request.PostId, admin.GraduationYear, post.YearGroups ?? new List<int>(), post.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");
            }

            var updatedCommunityId = admin.ResolveCommunityForCreation(request.CommunityId);
            var updatedYearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            ScopeAuthorizationExtensions.NormalizeAudience(ref updatedYearGroups, ref updatedCommunityId);
            post.CommunityId = updatedCommunityId;

            post.Title = request.Title;
            post.Content = request.Content;
            post.Category = request.Category;
            post.IsPinned = request.IsPinned;
            if (request.Status == "Published" && post.Status != "Published")
                post.PublishedAt = DateTime.UtcNow;
            post.Status = request.Status;
            post.YearGroups = updatedYearGroups;
            post.YoutubeVideoUrls = request.YoutubeVideoUrls;

            var imageUrls = new List<string>();
            if (request.ExistingImageUrls is { Count: > 0 })
                imageUrls.AddRange(request.ExistingImageUrls);
            if (request.Images is { Count: > 0 })
                imageUrls.AddRange(await storageService.BulkUploadFilesAsync(request.Images, institutionSlug: currentTenant.InstitutionSlug ?? ""));
            post.ImageUrls = imageUrls.Count > 0 ? imageUrls : null;

            post.UpdatedAt = DateTime.UtcNow;
            post.UpdatedBy = admin.Id;
            await newsRepo.UpdateAsync(post);
            logger.LogInformation("Post {PostId} updated by admin {AdminId}", post.Id, admin.Id);
            return post.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating post {PostId} by admin {AdminId}", request.PostId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<NewsPostDto>("Failed to update post");
        }
    }

    public async Task<IApiResponse<NewsPostDto>> PublishPostAsync(string postId, AuthData admin)
    {
        try
        {
            logger.LogInformation("PublishPost request for postId: {PostId} by admin {AdminId}", postId, admin.Id);

            var post = await newsRepo.GetByIdAsync(postId);
            if (post is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");

            if (!admin.CanModifyScopedItem(post.YearGroups, post.CreatedBy, post.CommunityId))
            {
                logger.LogWarning("Denied news post publish access for admin {AdminId} to post {PostId} (adminYear={AdminYear}, postYears={PostYears}, createdBy={CreatedBy})",
                    admin.Id, postId, admin.GraduationYear, post.YearGroups ?? new List<int>(), post.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");
            }

            post.Status = "Published";
            post.PublishedAt = DateTime.UtcNow;
            post.UpdatedAt = DateTime.UtcNow;
            post.UpdatedBy = admin.Id;
            await newsRepo.UpdateAsync(post);

            logger.LogInformation("Post {PostId} published by admin {AdminId}", postId, admin.Id);
            return post.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error publishing post {PostId} by admin {AdminId}", postId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<NewsPostDto>("Failed to publish post");
        }
    }

    public async Task<IApiResponse<object>> DeletePostAsync(string postId, AuthData admin)
    {
        try
        {
            logger.LogInformation("DeletePost request for postId: {PostId} by admin {AdminId}", postId, admin.Id);

            var post = await newsRepo.GetByIdAsync(postId);
            if (post is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Post not found");

            if (!admin.CanModifyScopedItem(post.YearGroups, post.CreatedBy, post.CommunityId))
            {
                logger.LogWarning("Denied news post delete access for admin {AdminId} to post {PostId} (adminYear={AdminYear}, postYears={PostYears}, createdBy={CreatedBy})",
                    admin.Id, postId, admin.GraduationYear, post.YearGroups ?? new List<int>(), post.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Post not found");
            }

            await newsRepo.RemoveAsync(post);
            logger.LogInformation("Post {PostId} deleted", postId);
            return new object().ToOkApiResponse("Post deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting post {PostId}", postId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete post");
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
