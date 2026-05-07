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

public class MemberForumService(
    IAlumniPgRepository<ForumCategory> categoryRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    IAlumniPgRepository<ForumPost> postRepo,
    ILogger<MemberForumService> logger) : IMemberForumService
{
    public async Task<IApiResponse<PgPagedResult<ForumCategoryDto>>> GetCategoriesAsync()
    {
        try
        {
            logger.LogInformation("GetForumCategories request");
            var result = await categoryRepo.GetPagedAsync(1, 100, "Name", "asc");
            var dtoResult = new PgPagedResult<ForumCategoryDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(c => c.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving forum categories");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ForumCategoryDto>>("Failed to retrieve categories");
        }
    }

    public async Task<IApiResponse<PgPagedResult<ForumThreadDto>>> GetThreadsAsync(ForumThreadFilter filter)
    {
        try
        {
            logger.LogInformation("GetThreads request — filter: {Filter}", filter.Serialize());
            var result = await threadRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "IsPinned", filter.SortDir ?? "desc",
                t => !t.IsClosed
                  && (string.IsNullOrEmpty(filter.CategoryId) || t.CategoryId == filter.CategoryId)
                  && (string.IsNullOrEmpty(filter.Search) || t.Title.Contains(filter.Search))
                  && (string.IsNullOrEmpty(filter.Filter)
                      || (filter.Filter == "pinned" && t.IsPinned)
                      || (filter.Filter == "recent")
                      || (filter.Filter == "popular")
                      || (filter.Filter == "all"))
            );
            var dtoResult = new PgPagedResult<ForumThreadDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(t => t.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving threads — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ForumThreadDto>>("Failed to retrieve threads");
        }
    }

    public async Task<IApiResponse<ForumThreadDto>> CreateThreadAsync(CreateThreadRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("CreateThread request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            var category = await categoryRepo.GetByIdAsync(request.CategoryId);
            if (category is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ForumThreadDto>("Category not found");

            var authorSnap = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl };
            var thread = new ForumThread
            {
                CategoryId = request.CategoryId,
                Category = new ForumCategorySnapshot { Id = category.Id, Name = category.Name, Description = category.Description, SortOrder = category.SortOrder },
                Title = request.Title,
                AuthorId = member.Id,
                Author = authorSnap,
                CreatedBy = member.Id,
            };
            await threadRepo.AddAsync(thread);

            var post = new ForumPost
            {
                ThreadId = thread.Id,
                Thread = new ForumThreadSnapshot { Id = thread.Id, Title = thread.Title },
                AuthorId = member.Id,
                Author = authorSnap,
                Content = request.Content,
                CreatedBy = member.Id,
            };
            await postRepo.AddAsync(post);

            logger.LogInformation("Thread {ThreadId} created by member {MemberId}", thread.Id, member.Id);
            return thread.ToDto().ToCreatedApiResponse("Thread created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating thread for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ForumThreadDto>("Failed to create thread");
        }
    }

    public async Task<IApiResponse<PgPagedResult<ForumPostDto>>> GetPostsAsync(string threadId, BaseFilter filter)
    {
        try
        {
            logger.LogInformation("GetForumPosts for threadId: {ThreadId} — filter: {Filter}", threadId, filter.Serialize());
            var result = await postRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "asc",
                p => p.ThreadId == threadId && !p.IsDeleted);
            var dtoResult = new PgPagedResult<ForumPostDto>
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
            logger.LogError(e, "Error retrieving posts for thread {ThreadId}", threadId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ForumPostDto>>("Failed to retrieve posts");
        }
    }

    public async Task<IApiResponse<ForumPostDto>> ReplyToThreadAsync(CreateForumPostRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("ReplyToThread request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            var thread = await threadRepo.GetByIdAsync(request.ThreadId);
            if (thread is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ForumPostDto>("Thread not found");
            if (thread.IsClosed)
                return ApiResponseExtensions.ToBadRequestApiResponse<ForumPostDto>("Thread is closed");

            var post = new ForumPost
            {
                ThreadId = request.ThreadId,
                Thread = new ForumThreadSnapshot { Id = thread.Id, Title = thread.Title },
                AuthorId = member.Id,
                Author = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl },
                Content = request.Content,
                CreatedBy = member.Id,
            };
            await postRepo.AddAsync(post);

            thread.ReplyCount += 1;
            await threadRepo.UpdateAsync(thread);

            logger.LogInformation("Reply {PostId} added to thread {ThreadId} by member {MemberId}", post.Id, request.ThreadId, member.Id);
            return post.ToDto().ToCreatedApiResponse("Reply posted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error replying to thread {ThreadId} by member {MemberId}", request.ThreadId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ForumPostDto>("Failed to post reply");
        }
    }
}
