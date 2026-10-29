using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class MemberForumService(
    IAlumniPgRepository<ForumCategory> categoryRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    IAlumniPgRepository<ForumPost> postRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    ILogger<MemberForumService> logger,
    IAlumniPgRepository<ForumPostHelpful>? helpfulRepo = null) : IMemberForumService
{
    /// <summary>True if this member is an approved member or leader of the given community.</summary>
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

    /// <summary>
    /// What the list needs to make a thread worth opening: the start of what was said, and when and by whom it was last answered.
    /// Two small grouped queries for the whole page, never one per thread.
    /// </summary>
    private async Task AddPreviewsAsync(IEnumerable<ForumThreadDto> results)
    {
        var threads = results.ToList();
        if (threads.Count == 0) return;
        var ids = threads.Select(t => t.Id).ToList();

        var firsts = await postRepo.GetQueryable(p => ids.Contains(p.ThreadId) && !p.IsDeleted)
            .GroupBy(p => p.ThreadId)
            .Select(g => g.OrderBy(p => p.CreatedAt).Select(p => new { p.ThreadId, Text = p.Content.Length > 400 ? p.Content.Substring(0, 400) : p.Content }).First())
            .ToListAsync();
        var lasts = await postRepo.GetQueryable(p => ids.Contains(p.ThreadId) && !p.IsDeleted)
            .GroupBy(p => p.ThreadId)
            .Select(g => g.OrderByDescending(p => p.CreatedAt).Select(p => new { p.ThreadId, p.AuthorId, p.CreatedAt }).First())
            .ToListAsync();

        var replierIds = lasts.Select(l => l.AuthorId).Distinct().ToList();
        var repliers = (await memberRepo.GetQueryable(m => replierIds.Contains(m.Id)).Card().ToListAsync()).ToDictionary(m => m.Id);
        var firstText = firsts.ToDictionary(f => f.ThreadId, f => f.Text);
        var lastPost = lasts.ToDictionary(l => l.ThreadId);

        foreach (var t in threads)
        {
            if (firstText.TryGetValue(t.Id, out var text))
            {
                var plain = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
                t.Excerpt = plain.Length > 160 ? plain[..160].TrimEnd() + "…" : plain;
            }
            t.LastActivityAt = lastPost.TryGetValue(t.Id, out var last) ? last.CreatedAt : t.CreatedAt;
            if (t.ReplyCount > 0 && lastPost.TryGetValue(t.Id, out last) && repliers.TryGetValue(last.AuthorId, out var who))
                t.LastReplyByName = $"{who.FirstName} {who.LastName}".Trim();
        }
    }

    private async Task EnrichCommunityNamesAsync(IEnumerable<ForumThreadDto> results)
    {
        var communityIds = results.Where(d => d.CommunityId != null).Select(d => d.CommunityId!).Distinct().ToList();
        if (communityIds.Count == 0) return;
        var communities = await communityRepo.GetAllAsync(c => communityIds.Contains(c.Id));
        var nameById = communities.ToDictionary(c => c.Id, c => c.Name);
        foreach (var dto in results)
            if (dto.CommunityId != null && nameById.TryGetValue(dto.CommunityId, out var name))
                dto.CommunityName = name;
    }

    public async Task<IApiResponse<PgPagedResult<ForumCategoryDto>>> GetCategoriesAsync()
    {
        try
        {
            logger.LogInformation("GetForumCategories request");
            var result = await categoryRepo.GetPagedAsync(1, 100, "SortOrder", "asc");
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

    public async Task<IApiResponse<PgPagedResult<ForumThreadDto>>> GetThreadsAsync(ForumThreadFilter filter, AuthData member)
    {
        try
        {
            logger.LogInformation("GetThreads request — filter: {Filter}", filter.Serialize());

            if (!string.IsNullOrEmpty(filter.CommunityId) && !await IsApprovedCommunityMemberAsync(filter.CommunityId, member.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<ForumThreadDto>>("You must be an approved member of this community to view its forum");

            // No explicit CommunityId means "everything I can see" — institution-wide
            // plus every community I'm an approved member of — never someone else's community.
            var approvedCommunityIds = string.IsNullOrEmpty(filter.CommunityId) ? await GetApprovedCommunityIdsAsync(member.Id) : [];

            var search = filter.Search?.ToLower();
            var result = await threadRepo.GetPagedAsync(
                // Recent and Popular used to only filter nothing out, so the tabs looked the same. Each now orders the list as it says.
                filter.Page, filter.PageSize, filter.SortColumn ?? filter.Filter switch { "recent" => "CreatedAt", "popular" => "ReplyCount", "unanswered" => "CreatedAt", _ => "IsPinned" }, filter.SortDir ?? "desc",
                t => !t.IsClosed
                  && (string.IsNullOrEmpty(filter.CommunityId)
                      ? (t.CommunityId == null || approvedCommunityIds.Contains(t.CommunityId))
                      : t.CommunityId == filter.CommunityId)
                  && (string.IsNullOrEmpty(filter.CategoryId) || t.CategoryId == filter.CategoryId)
                  && TextSearch.Matches(search, t.Title)
                  && (string.IsNullOrEmpty(filter.Filter)
                      || (filter.Filter == "pinned" && t.IsPinned)
                      || (filter.Filter == "unanswered" && t.ReplyCount == 0)
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
            await EnrichCommunityNamesAsync(dtoResult.Results);

            // ToDto() reads the author's name/photo from the MemberSnapshot frozen on
            // the ForumThread at creation time — refresh both from the live Member
            // records here (one batched lookup, not one query per row) so a later
            // name/photo change actually shows up. Falls back to the snapshot if the
            // author has since been deleted.
            var authorIds = dtoResult.Results.Select(d => d.AuthorId).Distinct().ToList();
            var authors = (await memberRepo.GetQueryable(m => authorIds.Contains(m.Id)).Card().ToListAsync()).ToDictionary(m => m.Id);
            foreach (var dto in dtoResult.Results)
            {
                if (authors.TryGetValue(dto.AuthorId, out var author))
                {
                    dto.AuthorName = $"{author.FirstName} {author.LastName}";
                    dto.AuthorProfilePictureUrl = author.ProfilePictureUrl;
                }
            }

            await AddPreviewsAsync(dtoResult.Results);

            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving threads — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ForumThreadDto>>("Failed to retrieve threads");
        }
    }

    public async Task<IApiResponse<ForumThreadDto>> GetThreadByIdAsync(string threadId, AuthData member)
    {
        try
        {
            logger.LogInformation("GetThreadById request for thread {ThreadId} by member {MemberId}", threadId, member.Id);

            var thread = await threadRepo.GetByIdAsync(threadId);
            if (thread is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ForumThreadDto>("Thread not found");

            if (!string.IsNullOrEmpty(thread.CommunityId) && !await IsApprovedCommunityMemberAsync(thread.CommunityId, member.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<ForumThreadDto>("You must be an approved member of this community to view its forum");

            var dto = thread.ToDto();
            var author = await memberRepo.GetByIdAsync(thread.AuthorId);
            if (author is not null)
            {
                dto.AuthorName = $"{author.FirstName} {author.LastName}";
                dto.AuthorProfilePictureUrl = author.ProfilePictureUrl;
            }

            return dto.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving thread {ThreadId}", threadId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ForumThreadDto>("Failed to retrieve thread");
        }
    }

    public async Task<IApiResponse<ForumThreadDto>> CreateThreadAsync(CreateThreadRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("CreateThread request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            var thread = new ForumThread
            {
                Title = request.Title,
                AuthorId = member.Id,
                CreatedBy = member.Id,
            };

            if (!string.IsNullOrEmpty(request.CommunityId))
            {
                if (!await IsApprovedCommunityMemberAsync(request.CommunityId, member.Id))
                    return ApiResponseExtensions.ToForbiddenApiResponse<ForumThreadDto>("You must be an approved member of this community to post here");
                thread.CommunityId = request.CommunityId;
            }
            else
            {
                if (string.IsNullOrEmpty(request.CategoryId))
                    return ApiResponseExtensions.ToBadRequestApiResponse<ForumThreadDto>("Category is required");
                var category = await categoryRepo.GetByIdAsync(request.CategoryId);
                if (category is null)
                    return ApiResponseExtensions.ToNotFoundApiResponse<ForumThreadDto>("Category not found");
                thread.CategoryId = request.CategoryId;
                thread.Category = new ForumCategorySnapshot { Id = category.Id, Name = category.Name, Description = category.Description, SortOrder = category.SortOrder };
            }

            var authorSnap = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl };
            thread.Author = authorSnap;
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

    public async Task<IApiResponse<object>> ToggleHelpfulAsync(string postId, AuthData member)
    {
        try
        {
            if (helpfulRepo is null) return ApiResponseExtensions.ToServerErrorApiResponse<object>("Helpful marks are not available");
            var post = await postRepo.GetByIdAsync(postId);
            if (post is null || post.IsDeleted)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Reply not found");
            var thread = await threadRepo.GetByIdAsync(post.ThreadId);
            if (thread is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Reply not found");
            if (!string.IsNullOrEmpty(thread.CommunityId) && !await IsApprovedCommunityMemberAsync(thread.CommunityId, member.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("You must be an approved member of this community to do that");
            if (post.AuthorId == member.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You cannot mark your own reply helpful");

            var existing = await helpfulRepo.GetOneAsync(h => h.PostId == postId && h.MemberId == member.Id);
            if (existing is not null) await helpfulRepo.RemoveAsync(existing);
            else await helpfulRepo.AddAsync(new ForumPostHelpful { PostId = postId, ThreadId = post.ThreadId, MemberId = member.Id, CreatedBy = member.Id });

            var count = await helpfulRepo.CountAsync(h => h.PostId == postId);
            return ((object)new { Marked = existing is null, HelpfulCount = count }).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error toggling helpful on post {PostId} for member {MemberId}", postId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to update that");
        }
    }

    public async Task<IApiResponse<PgPagedResult<ForumPostDto>>> GetPostsAsync(string threadId, BaseFilter filter, AuthData member)
    {
        try
        {
            logger.LogInformation("GetForumPosts for threadId: {ThreadId} — filter: {Filter}", threadId, filter.Serialize());

            var parentThread = await threadRepo.GetByIdAsync(threadId);
            if (parentThread is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PgPagedResult<ForumPostDto>>("Thread not found");
            if (!string.IsNullOrEmpty(parentThread.CommunityId) && !await IsApprovedCommunityMemberAsync(parentThread.CommunityId, member.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<ForumPostDto>>("You must be an approved member of this community to view this thread");

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

            // How helpful each reply was found, and whether the caller marked it: one grouped query for the page.
            if (helpfulRepo is not null && dtoResult.Results.Any())
            {
                var postIds = dtoResult.Results.Select(d => d.Id).ToList();
                var marks = await helpfulRepo.GetQueryable(h => postIds.Contains(h.PostId)).Select(h => new { h.PostId, h.MemberId }).ToListAsync();
                var counts = marks.GroupBy(m => m.PostId).ToDictionary(g => g.Key, g => g.Count());
                var mine = marks.Where(m => m.MemberId == member.Id).Select(m => m.PostId).ToHashSet();
                foreach (var dto in dtoResult.Results)
                {
                    dto.HelpfulCount = counts.GetValueOrDefault(dto.Id);
                    dto.MarkedHelpfulByMe = mine.Contains(dto.Id);
                }
            }

            // Same staleness fix as GetThreadsAsync above — batched, not per-row.
            var authorIds = dtoResult.Results.Select(d => d.AuthorId).Distinct().ToList();
            var authors = (await memberRepo.GetQueryable(m => authorIds.Contains(m.Id)).Card().ToListAsync()).ToDictionary(m => m.Id);
            foreach (var dto in dtoResult.Results)
            {
                if (authors.TryGetValue(dto.AuthorId, out var author))
                {
                    dto.AuthorName = $"{author.FirstName} {author.LastName}";
                    dto.AuthorProfilePictureUrl = author.ProfilePictureUrl;
                }
            }

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
            if (!string.IsNullOrEmpty(thread.CommunityId) && !await IsApprovedCommunityMemberAsync(thread.CommunityId, member.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<ForumPostDto>("You must be an approved member of this community to reply here");

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

            // Notify the thread's original author only (not every prior
            // participant) — skip when they're replying to their own thread.
            if (thread.AuthorId != member.Id)
            {
                await temporalProvider.EnqueueNotificationAsync(
                    NotificationRequest.ForumReply(
                        currentTenant.InstitutionId!, thread.AuthorId, $"{member.FirstName} {member.LastName}", thread.Title, thread.Id),
                    logger);
            }

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
