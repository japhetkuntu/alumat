using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Text;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class ManageContentService(
    GroupGuard guard,
    GroupMedia media,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<Resource> resourceRepo,
    IAlumniPgRepository<InstitutionAuditLogEntry> auditRepo,
    ICurrentTenantService currentTenant,
    ITemporalClientProvider temporalProvider,
    ILogger<ManageContentService> logger) : IManageContentService
{
    private const int MaxListed = 100;
    private static readonly string[] JobTypes = ["Full-time", "Part-time", "Contract", "Internship"];
    private static readonly string[] NewsCategories = ["Announcement", "Achievement", "News", "Event", "Opportunity", "Other"];

    // ── News ─────────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<GroupNewsDto>>> GetNewsAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<List<GroupNewsDto>>(memberId, LeadershipPrivileges.PostNews, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var communityScope = group!.Community?.Id;
            var posts = (await newsRepo.GetAllAsync(n => n.CommunityId == communityScope)).Where(n => group!.Owns(n.CommunityId, n.YearGroups));
            return posts.OrderByDescending(n => n.PublishedAt ?? n.CreatedAt).Take(MaxListed).Select(n => ToDto(n, memberId)).ToList().ToOkApiResponse();
        }
        catch (Exception e) { return Fail<List<GroupNewsDto>>(e, "list news", "Failed to load the news"); }
    }

    public async Task<IApiResponse<GroupNewsDto>> CreateNewsAsync(string memberId, string scopeType, string scopeId, SaveGroupNewsRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupNewsDto>(memberId, LeadershipPrivileges.PostNews, scopeType, scopeId);
            if (refusal is not null) return refusal;
            if (CleanNews(request, out var title, out var content, out var category) is { } problem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupNewsDto>(problem);
            if (GroupMedia.CleanVideos(request.YoutubeVideoUrls, out var videos) is { } videoProblem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupNewsDto>(videoProblem);
            if (await GroupMedia.CheckAsync(null, request.Images) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupNewsDto>(picture);

            var author = await memberRepo.GetByIdAsync(memberId);
            var (communityId, years) = group!.Audience;
            var post = new NewsPost
            {
                CommunityId = communityId, YearGroups = years, Title = title, Content = content, Category = category,
                Status = "Published", PublishedAt = DateTime.UtcNow, IsPublic = false, AuthorId = memberId, CreatedBy = memberId, YoutubeVideoUrls = videos,
                Author = author is null ? null : new MemberSnapshot { Id = author.Id, FirstName = author.FirstName, LastName = author.LastName, Email = author.Email, ProfilePictureUrl = author.ProfilePictureUrl, MemberNumber = author.MemberNumber },
            };
            var pictures = await media.UploadAllAsync(request.Images);
            post.ImageUrls = pictures.Count > 0 ? pictures : null;
            await newsRepo.AddAsync(post);
            await AuditAsync(memberId, "News Posted", $"{title}, {group.Name}");
            return ToDto(post, memberId).ToCreatedApiResponse("Posted");
        }
        catch (Exception e) { return Fail<GroupNewsDto>(e, "post news", "Failed to post the news"); }
    }

    public async Task<IApiResponse<GroupNewsDto>> UpdateNewsAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupNewsRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupNewsDto>(memberId, LeadershipPrivileges.PostNews, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var post = await OwnNewsAsync(group!, memberId, id);
            if (post is null) return ApiResponseExtensions.ToNotFoundApiResponse<GroupNewsDto>("That post was not found");
            if (CleanNews(request, out var title, out var content, out var category) is { } problem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupNewsDto>(problem);
            if (GroupMedia.CleanVideos(request.YoutubeVideoUrls, out var videos) is { } videoProblem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupNewsDto>(videoProblem);
            var kept = GroupMedia.Keep(post.ImageUrls, request.ExistingImageUrls, []);
            if (await GroupMedia.CheckAsync(null, request.Images, kept.Count) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupNewsDto>(picture);

            var all = kept.Concat(await media.UploadAllAsync(request.Images)).ToList();
            post.ImageUrls = all.Count > 0 ? all : null; post.YoutubeVideoUrls = videos;
            post.Title = title; post.Content = content; post.Category = category; post.UpdatedAt = DateTime.UtcNow; post.UpdatedBy = memberId;
            await newsRepo.UpdateAsync(post);
            await AuditAsync(memberId, "News Edited", $"{title}, {group!.Name}");
            return ToDto(post, memberId).ToOkApiResponse("Saved");
        }
        catch (Exception e) { return Fail<GroupNewsDto>(e, "edit news", "Failed to save the post"); }
    }

    public async Task<IApiResponse<object>> DeleteNewsAsync(string memberId, string scopeType, string scopeId, string id)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<object>(memberId, LeadershipPrivileges.PostNews, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var post = await OwnNewsAsync(group!, memberId, id);
            if (post is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("That post was not found");
            await newsRepo.RemoveAsync(post);
            await AuditAsync(memberId, "News Deleted", $"{post.Title}, {group!.Name}");
            return new object().ToOkApiResponse("Deleted");
        }
        catch (Exception e) { return Fail<object>(e, "delete news", "Failed to delete the post"); }
    }

    private async Task<NewsPost?> OwnNewsAsync(GroupRef group, string memberId, string id)
        => (await newsRepo.GetAllAsync(n => n.Id == id)).FirstOrDefault(n => n.AuthorId == memberId && group.Owns(n.CommunityId, n.YearGroups));

    private static GroupNewsDto ToDto(NewsPost n, string memberId) => new(n.Id, n.Title, n.Content, n.Category, n.PublishedAt, n.AuthorId == memberId, n.ImageUrls, n.YoutubeVideoUrls);

    private static string? CleanNews(SaveGroupNewsRequest r, out string title, out string content, out string category)
    {
        title = Squash(r.Title); content = ""; category = "General";
        if (title.Length == 0) return "Give the post a title";
        if (title.Length > 200) return "Keep the title to 200 characters";
        content = RichText.Sanitize(r.Content) ?? "";
        if (RichText.ToPlainText(content).Trim().Length == 0) return "Write something to share";
        if (RichText.IsTooLong(content)) return RichText.TooLongMessage;
        category = NewsCategories.FirstOrDefault(c => c.Equals(r.Category?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Announcement";
        return null;
    }

    // ── Jobs ─────────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<GroupJobDto>>> GetJobsAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<List<GroupJobDto>>(memberId, LeadershipPrivileges.PostJobs, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var communityScope = group!.Community?.Id;
            var jobs = (await jobRepo.GetAllAsync(j => j.CommunityId == communityScope)).Where(j => group!.Owns(j.CommunityId, j.YearGroups));
            return jobs.OrderByDescending(j => j.CreatedAt).Take(MaxListed).Select(j => ToDto(j, memberId)).ToList().ToOkApiResponse();
        }
        catch (Exception e) { return Fail<List<GroupJobDto>>(e, "list jobs", "Failed to load the jobs"); }
    }

    public async Task<IApiResponse<GroupJobDto>> CreateJobAsync(string memberId, string scopeType, string scopeId, SaveGroupJobRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupJobDto>(memberId, LeadershipPrivileges.PostJobs, scopeType, scopeId);
            if (refusal is not null) return refusal;
            if (CleanJob(request, mustBeAhead: true, out var clean) is { } problem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupJobDto>(problem);
            if (await GroupMedia.CheckAsync(request.BannerImage, null) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupJobDto>(picture);

            var (communityId, years) = group!.Audience;
            var job = new Job
            {
                CommunityId = communityId, YearGroups = years, Title = clean.Title, Company = clean.Company, Location = clean.Location, Type = clean.Type,
                Description = clean.Description, ApplyUrl = clean.ApplyUrl, Deadline = clean.Deadline, Status = "Active", IsPublic = false, PostedBy = memberId, CreatedBy = memberId,
                BannerImageUrl = request.BannerImage is null ? null : await media.UploadAsync(request.BannerImage),
            };
            await jobRepo.AddAsync(job);
            await EnqueueAsync(NotificationRequest.JobAlert(currentTenant.InstitutionId ?? "", job.Id));
            await AuditAsync(memberId, "Job Posted", $"{job.Title} at {job.Company}, {group.Name}");
            return ToDto(job, memberId).ToCreatedApiResponse("Posted");
        }
        catch (Exception e) { return Fail<GroupJobDto>(e, "post job", "Failed to post the job"); }
    }

    public async Task<IApiResponse<GroupJobDto>> UpdateJobAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupJobRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupJobDto>(memberId, LeadershipPrivileges.PostJobs, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var job = await OwnJobAsync(group!, memberId, id);
            if (job is null) return ApiResponseExtensions.ToNotFoundApiResponse<GroupJobDto>("That job was not found");
            if (job.Status == "Closed") return ApiResponseExtensions.ToBadRequestApiResponse<GroupJobDto>("This job is closed");
            if (CleanJob(request, mustBeAhead: request.Deadline != job.Deadline, out var clean) is { } problem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupJobDto>(problem);
            if (await GroupMedia.CheckAsync(request.BannerImage, null) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupJobDto>(picture);
            job.BannerImageUrl = request.BannerImage is not null ? await media.UploadAsync(request.BannerImage) : request.RemoveBanner ? null : job.BannerImageUrl;

            job.Title = clean.Title; job.Company = clean.Company; job.Location = clean.Location; job.Type = clean.Type; job.Description = clean.Description;
            job.ApplyUrl = clean.ApplyUrl; job.Deadline = clean.Deadline; job.UpdatedAt = DateTime.UtcNow; job.UpdatedBy = memberId;
            await jobRepo.UpdateAsync(job);
            await AuditAsync(memberId, "Job Edited", $"{job.Title}, {group!.Name}");
            return ToDto(job, memberId).ToOkApiResponse("Saved");
        }
        catch (Exception e) { return Fail<GroupJobDto>(e, "edit job", "Failed to save the job"); }
    }

    public async Task<IApiResponse<object>> CloseJobAsync(string memberId, string scopeType, string scopeId, string id)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<object>(memberId, LeadershipPrivileges.PostJobs, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var job = await OwnJobAsync(group!, memberId, id);
            if (job is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("That job was not found");
            if (job.Status == "Closed") return ApiResponseExtensions.ToBadRequestApiResponse<object>("This job is already closed");
            job.Status = "Closed"; job.UpdatedAt = DateTime.UtcNow; job.UpdatedBy = memberId;
            await jobRepo.UpdateAsync(job);
            await AuditAsync(memberId, "Job Closed", $"{job.Title}, {group!.Name}");
            return new object().ToOkApiResponse("Closed");
        }
        catch (Exception e) { return Fail<object>(e, "close job", "Failed to close the job"); }
    }

    private async Task<Job?> OwnJobAsync(GroupRef group, string memberId, string id)
        => (await jobRepo.GetAllAsync(j => j.Id == id)).FirstOrDefault(j => j.PostedBy == memberId && group.Owns(j.CommunityId, j.YearGroups));

    private static GroupJobDto ToDto(Job j, string memberId) => new(j.Id, j.Title, j.Company, j.Location, j.Type, j.Description, j.ApplyUrl, j.Deadline, j.Status, j.PostedBy == memberId && j.Status != "Closed", j.BannerImageUrl);

    private sealed record CleanJobFields(string Title, string Company, string Location, string Type, string? Description, string? ApplyUrl, DateTime? Deadline);

    private static string? CleanJob(SaveGroupJobRequest r, bool mustBeAhead, out CleanJobFields clean)
    {
        clean = new("", "", "", "Full-time", null, null, null);
        var title = Squash(r.Title); var company = Squash(r.Company); var location = Squash(r.Location);
        if (title.Length == 0) return "Give the job a title";
        if (company.Length == 0) return "Say who the job is with";
        if (location.Length == 0) return "Say where it is, or Remote";
        if (title.Length > 150 || company.Length > 150 || location.Length > 150) return "Keep the title, company and place to 150 characters each";
        var type = JobTypes.FirstOrDefault(t => t.Equals(r.Type?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (type is null) return "Choose full-time, part-time, contract or internship";
        string? applyUrl = null;
        if (!string.IsNullOrWhiteSpace(r.ApplyUrl))
        {
            applyUrl = r.ApplyUrl.Trim();
            if (!IsWebAddress(applyUrl)) return "The apply link must be a web address starting with https://";
        }
        DateTime? deadline = r.Deadline.HasValue ? DateTime.SpecifyKind(r.Deadline.Value, DateTimeKind.Utc) : null;
        if (mustBeAhead && deadline.HasValue && deadline.Value < DateTime.UtcNow) return "Choose a closing date that is still ahead";
        var description = RichText.Sanitize(r.Description);
        if (RichText.IsTooLong(description)) return RichText.TooLongMessage;
        clean = new(title, company, location, type, description, applyUrl, deadline);
        return null;
    }

    // ── Resources (links) ────────────────────────────────────────────────────

    public async Task<IApiResponse<List<GroupResourceDto>>> GetResourcesAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<List<GroupResourceDto>>(memberId, LeadershipPrivileges.ShareResources, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var communityScope = group!.Community?.Id;
            var items = (await resourceRepo.GetAllAsync(r => r.CommunityId == communityScope)).Where(r => group!.Owns(r.CommunityId, r.YearGroups));
            return items.OrderByDescending(r => r.CreatedAt).Take(MaxListed).Select(r => ToDto(r, memberId)).ToList().ToOkApiResponse();
        }
        catch (Exception e) { return Fail<List<GroupResourceDto>>(e, "list resources", "Failed to load the resources"); }
    }

    public async Task<IApiResponse<GroupResourceDto>> CreateResourceAsync(string memberId, string scopeType, string scopeId, SaveGroupResourceRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupResourceDto>(memberId, LeadershipPrivileges.ShareResources, scopeType, scopeId);
            if (refusal is not null) return refusal;
            if (CleanResource(request, out var title, out var description, out var category, out var url) is { } problem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupResourceDto>(problem);
            if (await GroupMedia.CheckAsync(request.BannerImage, null) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupResourceDto>(picture);

            var (communityId, years) = group!.Audience;
            var item = new Resource { CommunityId = communityId, YearGroups = years, Title = title, Description = description, Category = category, Type = "Link", ExternalUrl = url, UploadedBy = memberId, CreatedBy = memberId,
                BannerImageUrl = request.BannerImage is null ? null : await media.UploadAsync(request.BannerImage) };
            await resourceRepo.AddAsync(item);
            await AuditAsync(memberId, "Resource Shared", $"{title}, {group.Name}");
            return ToDto(item, memberId).ToCreatedApiResponse("Shared");
        }
        catch (Exception e) { return Fail<GroupResourceDto>(e, "share resource", "Failed to share the link"); }
    }

    public async Task<IApiResponse<GroupResourceDto>> UpdateResourceAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupResourceRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupResourceDto>(memberId, LeadershipPrivileges.ShareResources, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var item = await OwnResourceAsync(group!, memberId, id);
            if (item is null) return ApiResponseExtensions.ToNotFoundApiResponse<GroupResourceDto>("That link was not found");
            if (CleanResource(request, out var title, out var description, out var category, out var url) is { } problem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupResourceDto>(problem);
            if (await GroupMedia.CheckAsync(request.BannerImage, null) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupResourceDto>(picture);
            item.BannerImageUrl = request.BannerImage is not null ? await media.UploadAsync(request.BannerImage) : request.RemoveBanner ? null : item.BannerImageUrl;

            item.Title = title; item.Description = description; item.Category = category; item.ExternalUrl = url; item.UpdatedAt = DateTime.UtcNow; item.UpdatedBy = memberId;
            await resourceRepo.UpdateAsync(item);
            await AuditAsync(memberId, "Resource Edited", $"{title}, {group!.Name}");
            return ToDto(item, memberId).ToOkApiResponse("Saved");
        }
        catch (Exception e) { return Fail<GroupResourceDto>(e, "edit resource", "Failed to save the link"); }
    }

    public async Task<IApiResponse<object>> DeleteResourceAsync(string memberId, string scopeType, string scopeId, string id)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<object>(memberId, LeadershipPrivileges.ShareResources, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var item = await OwnResourceAsync(group!, memberId, id);
            if (item is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("That link was not found");
            await resourceRepo.RemoveAsync(item);
            await AuditAsync(memberId, "Resource Removed", $"{item.Title}, {group!.Name}");
            return new object().ToOkApiResponse("Removed");
        }
        catch (Exception e) { return Fail<object>(e, "remove resource", "Failed to remove the link"); }
    }

    /// <summary>A link the leader shared, in this group. Files uploaded by administrators are not the leader's to change.</summary>
    private async Task<Resource?> OwnResourceAsync(GroupRef group, string memberId, string id)
        => (await resourceRepo.GetAllAsync(r => r.Id == id)).FirstOrDefault(r => r.UploadedBy == memberId && r.Type == "Link" && group.Owns(r.CommunityId, r.YearGroups));

    private static GroupResourceDto ToDto(Resource r, string memberId) => new(r.Id, r.Title, r.Description, r.Category, r.ExternalUrl ?? "", r.UploadedBy == memberId && r.Type == "Link", r.BannerImageUrl);

    private static string? CleanResource(SaveGroupResourceRequest r, out string title, out string? description, out string category, out string url)
    {
        title = Squash(r.Title); description = null; category = "General"; url = (r.ExternalUrl ?? "").Trim();
        if (title.Length == 0) return "Give the link a title";
        if (title.Length > 150) return "Keep the title to 150 characters";
        if (!IsWebAddress(url)) return "Paste a web address starting with https://";
        description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        if (description is { Length: > 500 }) return "Keep the description to 500 characters";
        var c = Squash(r.Category ?? "");
        if (c.Length > 60) return "Keep the category to 60 characters";
        if (c.Length > 0) category = c;
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Squash(string? text) => string.Join(" ", (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool IsWebAddress(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    private IApiResponse<T> Fail<T>(Exception e, string what, string message)
    {
        logger.LogError(e, "Error trying to {What}", what);
        return ApiResponseExtensions.ToServerErrorApiResponse<T>(message);
    }

    private async Task EnqueueAsync(NotificationRequest request)
    {
        try { await temporalProvider.EnqueueNotificationAsync(request, logger); }
        catch (Exception e) { logger.LogWarning(e, "Could not queue a {Kind} notification", request.Kind); }
    }

    private async Task AuditAsync(string memberId, string action, string target)
    {
        try
        {
            var who = await memberRepo.GetByIdAsync(memberId) is { } m ? $"{m.FirstName} {m.LastName}".Trim() : "A leader";
            await auditRepo.AddAsync(new InstitutionAuditLogEntry { ActorId = memberId, Actor = $"{who} (leader)", Action = action, Target = target, CreatedBy = memberId });
        }
        catch (Exception e) { logger.LogWarning(e, "Could not record {Action} in the audit log", action); }
    }
}
