using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// The platform's onboarding view: per-institution activation scorecard, goal
/// and milestones, and the lead → live → activated funnel. The activation rules
/// themselves live in InstitutionActivationService (PostgresDb.Sdk) so the
/// Operations Worker's nudges and the institution's own checklist use exactly
/// the same definition.
/// </summary>
public class ActivationTrackingService(
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<OnboardingLead> leadRepo,
    IAlumniPgRepository<PlatformSettings> platformSettingsRepo,
    InstitutionActivationService activation,
    IAuditLogService auditLog,
    ILogger<ActivationTrackingService> logger) : IActivationTrackingService
{
    public async Task<IApiResponse<ActivationScorecardResponse>> GetScorecardAsync()
    {
        try
        {
            return (await BuildScorecardAsync()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetScorecardAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<ActivationScorecardResponse>("Failed to load activation scorecard");
        }
    }

    public async Task<IApiResponse<ActivationScorecardItem>> GetInstitutionAsync(string institutionId)
    {
        try
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId);
            if (institution is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ActivationScorecardItem>("Institution not found");

            var result = await activation.EvaluateOneAsync(institution, DateTime.UtcNow);
            return ToItem(result!).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetInstitutionAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<ActivationScorecardItem>("Failed to load institution activation");
        }
    }

    public async Task<IApiResponse<ActivationFunnelResponse>> GetFunnelAsync(int weeks)
    {
        try
        {
            weeks = Math.Clamp(weeks, 1, 26);
            var now = DateTime.UtcNow;
            var firstWeek = InstitutionActivationService.WeekStart(now).AddDays(-7 * (weeks - 1));

            var leads = await leadRepo.GetQueryable()
                .Select(l => new { l.CreatedAt, l.ContactedAt })
                .ToListAsync();
            var institutions = await institutionRepo.GetQueryable()
                .Select(i => new { i.OnboardedAt, i.Status })
                .ToListAsync();

            var stages = new List<FunnelStage>
            {
                new("leads", "New leads", leads.Count),
                new("contacted", "Contacted", leads.Count(l => l.ContactedAt != null)),
                // Institutions rather than approved leads: some are created directly without a lead.
                new("live", "Live", institutions.Count(i => i.Status == "Active")),
            };

            int InWeek(IEnumerable<DateTime?> stamps, DateTime start) =>
                stamps.Count(t => t >= start && t < start.AddDays(7));

            var series = Enumerable.Range(0, weeks).Select(n =>
            {
                var start = firstWeek.AddDays(7 * n);
                return new FunnelWeek(start,
                    InWeek(leads.Select(l => (DateTime?)l.CreatedAt), start),
                    InWeek(leads.Select(l => l.ContactedAt), start),
                    InWeek(institutions.Select(i => (DateTime?)i.OnboardedAt), start));
            }).ToList();

            return new ActivationFunnelResponse(stages, series).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetFunnelAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<ActivationFunnelResponse>("Failed to load onboarding funnel");
        }
    }

    public async Task<IApiResponse<ActivationScorecardResponse>> UpdateTargetAsync(UpdateActivationTargetRequest request, string actorId, string actorName)
    {
        try
        {
            if (request.TargetCount is < 1)
                return ApiResponseExtensions.ToBadRequestApiResponse<ActivationScorecardResponse>("Target must be at least 1 institution");
            var milestones = request.Milestones ?? [];
            if (milestones.Any(m => m.LiveTarget is null && m.ActivatedTarget is null))
                return ApiResponseExtensions.ToBadRequestApiResponse<ActivationScorecardResponse>("Each milestone needs a live or an activated target");
            if (milestones.Any(m => m.LiveTarget is < 0 || m.ActivatedTarget is < 0))
                return ApiResponseExtensions.ToBadRequestApiResponse<ActivationScorecardResponse>("Milestone targets can't be negative");

            var settings = await platformSettingsRepo.GetOneAsync(s => s.Id == PlatformSettings.SingletonId);
            var isNew = settings is null;
            settings ??= new PlatformSettings { Id = PlatformSettings.SingletonId };
            settings.ActivationTargetCount = request.TargetCount;
            settings.ActivationTargetDate = request.TargetDate is { } d ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : null;
            settings.ActivationMilestones = milestones
                .OrderBy(m => m.Date)
                .Select(m => new ActivationMilestone
                {
                    Date = DateTime.SpecifyKind(m.Date.Date, DateTimeKind.Utc),
                    LiveTarget = m.LiveTarget,
                    ActivatedTarget = m.ActivatedTarget,
                })
                .ToList();
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedBy = actorId;
            if (isNew) await platformSettingsRepo.AddAsync(settings);
            else await platformSettingsRepo.UpdateAsync(settings);

            await auditLog.LogAsync(actorId, actorName,
                request.TargetCount is null
                    ? "cleared the activation target"
                    : $"set the activation target to {request.TargetCount} institutions{(settings.ActivationTargetDate is { } t ? $" by {t:d MMM yyyy}" : "")}"
                      + (settings.ActivationMilestones.Count > 0 ? $" with {settings.ActivationMilestones.Count} milestones" : ""),
                "Platform settings");

            return (await BuildScorecardAsync()).ToOkApiResponse("Target updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateTargetAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<ActivationScorecardResponse>("Failed to update activation target");
        }
    }

    public async Task<IApiResponse<ActivationScorecardItem>> UpdateInstitutionSettingsAsync(
        string institutionId, UpdateInstitutionActivationSettingsRequest request, string actorId, string actorName)
    {
        try
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId);
            if (institution is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ActivationScorecardItem>("Institution not found");

            institution.ActivationMinMembers = request.ActivationMinMembers;
            institution.SetupNudgesEnabled = request.SetupNudgesEnabled;
            institution.UpdatedAt = DateTime.UtcNow;
            institution.UpdatedBy = actorId;
            await institutionRepo.UpdateAsync(institution);

            await auditLog.LogAsync(actorId, actorName,
                $"set activation member threshold to {(request.ActivationMinMembers?.ToString() ?? $"the default ({InstitutionActivationService.MinMembers})")} and setup reminders {(request.SetupNudgesEnabled ? "on" : "off")}",
                institution.Name);

            var result = await activation.EvaluateOneAsync(institution, DateTime.UtcNow);
            return ToItem(result!).ToOkApiResponse("Activation settings updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateInstitutionSettingsAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<ActivationScorecardItem>("Failed to update activation settings");
        }
    }

    private static ActivationScorecardItem ToItem(InstitutionActivation r) => new(
        r.InstitutionId, r.InstitutionName, r.Slug, r.OnboardedAt, r.ActivatedAt, r.DaysLive,
        r.Criteria, r.MetCount, r.NextStep,
        r.IsStalled && r.ActivatedAt is null && !r.AllMet,
        // ActivatedAt is stamped by the worker's daily run; AllMet covers the gap until then.
        r.ActivatedAt is not null || r.AllMet,
        r.IsOverdue && !r.AllMet,
        r.MinMembers, r.TrialEndsAt, r.SetupNudgesEnabled);

    private async Task<ActivationScorecardResponse> BuildScorecardAsync()
    {
        var now = DateTime.UtcNow;
        var institutions = (await institutionRepo.GetAllAsync(i => i.Status == "Active")).ToList();
        var results = await activation.EvaluateAsync(institutions, now);
        var settings = await platformSettingsRepo.GetOneAsync(s => s.Id == PlatformSettings.SingletonId);

        var items = results
            .Select(ToItem)
            // Overdue and stalled first (they need a call), then least progress, then longest live.
            .OrderByDescending(i => i.IsOverdue)
            .ThenByDescending(i => i.IsStalled)
            .ThenBy(i => i.IsActivated)
            .ThenBy(i => i.MetCount)
            .ThenByDescending(i => i.DaysLive)
            .ToList();

        var milestones = (settings?.ActivationMilestones ?? [])
            .OrderBy(m => m.Date)
            .Select(m =>
            {
                var past = m.Date.AddDays(1) <= now;
                var asOf = past ? m.Date.AddDays(1) : now;
                var live = institutions.Count(i => i.OnboardedAt < asOf);
                var activated = institutions.Count(i => i.ActivatedAt != null && i.ActivatedAt < asOf);
                var met = (m.LiveTarget is null || live >= m.LiveTarget) && (m.ActivatedTarget is null || activated >= m.ActivatedTarget);
                return new MilestoneProgress(m.Date, m.LiveTarget, m.ActivatedTarget, live, activated,
                    met ? "met" : past ? "missed" : "open");
            })
            .ToList();

        return new ActivationScorecardResponse(
            items.Count,
            items.Count(i => i.IsActivated),
            items.Count(i => i.IsStalled),
            items.Count(i => i.IsOverdue),
            settings?.ActivationTargetCount,
            settings?.ActivationTargetDate,
            milestones,
            items);
    }
}
