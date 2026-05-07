using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Extensions;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using AdminEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Admin;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class MemberNewsService(
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<AdminEntity> adminRepo,
    ILogger<MemberNewsService> logger) : IMemberNewsService
{
    public async Task<IApiResponse<PgPagedResult<NewsPostDto>>> GetPostsAsync(NewsFilter filter)
    {
        try
        {
            logger.LogInformation("GetPosts request — filter: {Filter}", filter.Serialize());
            var result = await newsRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "PublishedAt", filter.SortDir ?? "desc",
                p => p.Status == "Published"
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
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving news posts — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<NewsPostDto>>("Failed to retrieve posts");
        }
    }

    public async Task<IApiResponse<NewsPostDto>> GetPostByIdAsync(string postId)
    {
        try
        {
            logger.LogInformation("GetPostById for postId: {PostId}", postId);

            var post = await newsRepo.GetByIdAsync(postId);
            if (post is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<NewsPostDto>("Post not found");

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
