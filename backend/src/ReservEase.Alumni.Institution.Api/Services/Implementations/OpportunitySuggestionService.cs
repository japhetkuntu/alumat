using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// The review step for opportunities members suggest. Reads and writes go through the tenant-filtered repositories, so an
/// administrator can only ever act on their own institution's suggestions, and only a posting that is still Pending can be
/// approved or declined (an already-live or closed one is left alone).
/// </summary>
public class OpportunitySuggestionService(
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    IInstitutionAuditLogService auditLog,
    ILogger<OpportunitySuggestionService> logger) : IOpportunitySuggestionService
{
    private async Task<(Job? Job, IApiResponse<T>? Problem)> LoadPendingAsync<T>(string jobId, AuthData admin)
    {
        var job = await jobRepo.GetByIdAsync(jobId);
        // A job the admin cannot see is reported as missing, the same as everywhere else in this portal.
        if (job is null || !admin.CanModifyScopedItem(job.YearGroups, job.CreatedBy, job.CommunityId))
            return (null, ApiResponseExtensions.ToNotFoundApiResponse<T>("Opportunity not found"));
        if (job.Status != "Pending")
            return (null, ApiResponseExtensions.ToBadRequestApiResponse<T>("This opportunity is not waiting for review"));
        return (job, null);
    }

    private async Task TellSuggesterAsync(Job job, string title, string body, string actionUrl, string admin)
    {
        // Only a member can have suggested it (staff post directly): an id that is not a member of this institution gets nothing.
        if (await memberRepo.GetByIdAsync(job.PostedBy) is null) return;
        await notificationRepo.AddAsync(new Notification
        {
            RecipientId = job.PostedBy, RecipientType = "Member", Title = title, Body = body, Type = "OpportunityReview",
            RelatedEntityId = job.Id, RelatedEntityType = "Job", ActionUrl = actionUrl, CreatedBy = admin,
        });
    }

    public async Task<IApiResponse<JobDto>> ApproveAsync(string jobId, AuthData admin)
    {
        try
        {
            var (job, problem) = await LoadPendingAsync<JobDto>(jobId, admin);
            if (job is null) return problem!;
            job.Status = "Active"; job.UpdatedAt = DateTime.UtcNow; job.UpdatedBy = admin.Id;
            await jobRepo.UpdateAsync(job);
            await TellSuggesterAsync(job, "Your suggestion is live", $"\"{job.Title}\" has been approved and shared with the community. Thank you.", $"/jobs/{job.Id}", admin.Id);
            await temporalProvider.EnqueueNotificationAsync(NotificationRequest.JobAlert(currentTenant.InstitutionId!, job.Id), logger);
            await auditLog.LogAsync(admin, "Opportunity Approved", job.Title);
            return job.ToDto().ToOkApiResponse("Approved and shared.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Approving opportunity {JobId} failed", jobId);
            return ApiResponseExtensions.ToServerErrorApiResponse<JobDto>("Failed to approve the opportunity");
        }
    }

    public async Task<IApiResponse<object>> DeclineAsync(string jobId, AuthData admin)
    {
        try
        {
            var (job, problem) = await LoadPendingAsync<object>(jobId, admin);
            if (job is null) return problem!;
            job.Status = "Closed"; job.UpdatedAt = DateTime.UtcNow; job.UpdatedBy = admin.Id;
            await jobRepo.UpdateAsync(job);
            await TellSuggesterAsync(job, "About your suggestion", $"Thank you for suggesting \"{job.Title}\". We are not able to share it with the community this time.", "/jobs", admin.Id);
            await auditLog.LogAsync(admin, "Opportunity Declined", job.Title);
            return new object().ToOkApiResponse("Declined.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Declining opportunity {JobId} failed", jobId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to decline the opportunity");
        }
    }

    public async Task NameSuggestersAsync(IEnumerable<JobDto> jobs)
    {
        var pending = jobs.Where(j => j.Status == "Pending").ToList();
        if (pending.Count == 0) return;
        var ids = (await jobRepo.GetQueryable(j => pending.Select(p => p.Id).Contains(j.Id)).Select(j => new { j.Id, j.PostedBy }).ToListAsync()).ToDictionary(x => x.Id, x => x.PostedBy);
        var members = ids.Values.Distinct().ToList();
        var names = (await memberRepo.GetQueryable(m => members.Contains(m.Id)).Select(m => new { m.Id, m.FirstName, m.LastName, m.GraduationYear }).ToListAsync())
            .ToDictionary(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim() + (m.GraduationYear > 0 ? $" (class of {m.GraduationYear})" : ""));
        foreach (var j in pending)
            if (ids.TryGetValue(j.Id, out var by) && names.TryGetValue(by, out var name)) j.SuggestedByName = name;
    }
}
