using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class ForumService(
    IAlumniPgRepository<ForumCategory> categoryRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    ILogger<ForumService> logger) : IForumService
{
    public async Task<IApiResponse<PgPagedResult<ForumCategoryDto>>> GetCategoriesAsync(BaseFilter filter)
    {
        try
        {
            logger.LogInformation("GetCategories request — filter: {Filter}", filter.Serialize());
            var result = await categoryRepo.GetPagedAsync(filter.Page, filter.PageSize, filter.SortColumn ?? "Name", filter.SortDir ?? "asc");
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

    public async Task<IApiResponse<ForumCategoryDto>> CreateCategoryAsync(string name, string? description, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<ForumCategoryDto>("Only super admins can manage forums");

            logger.LogInformation("CreateCategory request — name: {Name} by admin {AdminId}", name, admin.Id);

            var existing = await categoryRepo.GetOneAsync(c => c.Name == name);
            if (existing is not null)
                return ApiResponseExtensions.ToConflictApiResponse<ForumCategoryDto>("Category already exists");

            var category = new ForumCategory { Name = name, Description = description, CreatedBy = admin.Id };
            await categoryRepo.AddAsync(category);
            logger.LogInformation("ForumCategory {CategoryId} created by admin {AdminId}", category.Id, admin.Id);
            return category.ToDto().ToCreatedApiResponse("Category created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating forum category '{Name}' by admin {AdminId}", name, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ForumCategoryDto>("Failed to create category");
        }
    }

    public async Task<IApiResponse<PgPagedResult<ForumThreadDto>>> GetThreadsAsync(ForumThreadFilter filter)
    {
        try
        {
            logger.LogInformation("GetThreads request — filter: {Filter}", filter.Serialize());
            var result = await threadRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                t => (string.IsNullOrEmpty(filter.CategoryId) || t.CategoryId == filter.CategoryId)
                  && (string.IsNullOrEmpty(filter.Search) || t.Title.Contains(filter.Search))
                  && (string.IsNullOrEmpty(filter.Filter)
                      || (filter.Filter == "pinned" && t.IsPinned)
                      || (filter.Filter == "closed" && t.IsClosed)
                      || (filter.Filter == "recent")
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

    public async Task<IApiResponse<object>> PinThreadAsync(string threadId, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only super admins can manage forum threads");

            logger.LogInformation("PinThread request for threadId: {ThreadId} by admin {AdminId}", threadId, admin.Id);

            var thread = await threadRepo.GetByIdAsync(threadId);
            if (thread is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Thread not found");

            thread.IsPinned = !thread.IsPinned;
            thread.UpdatedAt = DateTime.UtcNow;
            await threadRepo.UpdateAsync(thread);

            logger.LogInformation("Thread {ThreadId} pin toggled to {IsPinned} by admin {AdminId}", threadId, thread.IsPinned, admin.Id);
            return new object().ToOkApiResponse(thread.IsPinned ? "Thread pinned" : "Thread unpinned");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error toggling pin for thread {ThreadId} by admin {AdminId}", threadId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to pin/unpin thread");
        }
    }

    public async Task<IApiResponse<object>> CloseThreadAsync(string threadId, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only super admins can manage forum threads");

            logger.LogInformation("CloseThread request for threadId: {ThreadId} by admin {AdminId}", threadId, admin.Id);

            var thread = await threadRepo.GetByIdAsync(threadId);
            if (thread is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Thread not found");

            thread.IsClosed = !thread.IsClosed;
            thread.UpdatedAt = DateTime.UtcNow;
            await threadRepo.UpdateAsync(thread);

            logger.LogInformation("Thread {ThreadId} close toggled to {IsClosed} by admin {AdminId}", threadId, thread.IsClosed, admin.Id);
            return new object().ToOkApiResponse(thread.IsClosed ? "Thread closed" : "Thread reopened");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error toggling close for thread {ThreadId} by admin {AdminId}", threadId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to close/reopen thread");
        }
    }

    public async Task<IApiResponse<object>> DeleteThreadAsync(string threadId, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only super admins can manage forum threads");

            logger.LogInformation("DeleteThread request for threadId: {ThreadId} by admin {AdminId}", threadId, admin.Id);

            var thread = await threadRepo.GetByIdAsync(threadId);
            if (thread is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Thread not found");

            await threadRepo.RemoveAsync(thread);
            logger.LogInformation("Thread {ThreadId} deleted by admin {AdminId}", threadId, admin.Id);
            return new object().ToOkApiResponse("Thread deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting thread {ThreadId}", threadId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete thread");
        }
    }
}
