using ReservEase.Alumni.Institution.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.Institution.Api.Engagement;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// The engagement workspace. Everything is read through the tenant-filtered repositories, so a reading can only ever be
/// about the signed-in institution. The expensive part (a couple of dozen aggregate queries) runs at most once every few
/// minutes per institution and period; recommendations and the day's health reading are written from that same pass.
/// </summary>
public class EngagementService(
    EngagementEngine engine,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<CommunityHealthSnapshot> snapshotRepo,
    IAlumniPgRepository<EngagementRecommendation> recommendationRepo,
    IAlumniPgRepository<EngagementChecklistEntry> checklistRepo,
    IAlumniPgRepository<StaffEntity> staffRepo,
    ICurrentTenantService currentTenant,
    IRedisService<InstitutionRedisConfig> cache,
    ILogger<EngagementService> logger) : IEngagementService
{
    private static readonly int[] AllowedPeriods = [7, 30, 90];
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public async Task<IApiResponse<EngagementDashboardDto>> GetDashboardAsync(AuthData admin, int days, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true)
    {
        try
        {
            if (!AllowedPeriods.Contains(days)) days = 30;
            var disabled = disabledFeatures.ToHashSet();
            var now = DateTime.UtcNow;
            var week = EngagementChecklist.WeekStart(now);

            // The computed reading is shared and cached; the checklist ticks are not (they change as people tick), so they are read fresh.
            var shared = await GetSharedAsync(days, disabled, usesYearGroups, now);

            var ticked = (await checklistRepo.GetQueryable(e => e.WeekStart == week).Select(e => e.ItemKey).ToListAsync()).ToHashSet();
            var open = await recommendationRepo.GetQueryable(r => r.Status == RecommendationStatuses.Open)
                .OrderBy(r => r.Priority == RecommendationPriorities.High ? 0 : r.Priority == RecommendationPriorities.Medium ? 1 : 2)
                .ThenByDescending(r => r.CreatedAt).Take(20).ToListAsync();

            var m = shared.Metrics;
            int? retention = m.PreviousParticipants >= new HealthScoreConfig().MinimumRetentionCohort
                ? (int)Math.Round(100m * m.RetainedParticipants / m.PreviousParticipants) : null;

            return new EngagementDashboardDto(
                days, now,
                new HealthDto(shared.Health.Score, shared.PreviousScore, shared.Health.Classification, shared.Health.Summary, shared.Health.Factors),
                m.ActiveMembers, m.PendingMembers, new Comparison(m.NewMembers, m.PreviousNewMembers),
                m.Activated, m.SignedInLast7Days, m.SignedInLast30Days,
                new Comparison(m.Participants, m.PreviousParticipants), retention, m.Dormant,
                m.ContributionVolume, m.PreviousContributionVolume, m.Contributors,
                m.UpcomingEvents.Count, m.ActiveCampaigns.Count, m.Weekly,
                open.Select(RecommendationDto.From).ToList(),
                EngagementChecklist.Build(m, disabled, ticked), week.ToString("yyyy-MM-dd"))
                .ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Engagement dashboard failed for {InstitutionId}", currentTenant.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<EngagementDashboardDto>("Failed to load the engagement workspace");
        }
    }


    // One computation at a time per institution and period. The page asks for the dashboard and the year groups together, and
    // without this both would compute the same reading (and try to record it) at once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    private async Task<SharedReading> GetSharedAsync(int days, ISet<string> disabled, bool usesYearGroups, DateTime now)
    {
        var key = $"engagement:{currentTenant.InstitutionId}:{days}:{usesYearGroups}:{string.Join(",", disabled.Order())}";
        var cached = await cache.GetAsync<SharedReading>(key);
        if (cached is not null) return cached;

        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            cached = await cache.GetAsync<SharedReading>(key); // whoever held the gate has probably just filled it
            if (cached is not null) return cached;
            var r = await engine.RefreshAsync(days, disabled, usesYearGroups, now);
            var shared = new SharedReading(r.Metrics, r.Health, r.PreviousScore, r.Cohorts, r.Ambassadors);
            await cache.SetAsync(key, shared, CacheFor);
            return shared;
        }
        finally { gate.Release(); }
    }

    private static CohortsDto ScopeCohorts(SharedReading r, AuthData admin, int days)
    {
        var all = admin.Role != StaffRoles.ScopedAdmin;
        var years = (admin.YearGroups ?? []).ToHashSet();
        var cohorts = all ? r.Cohorts : r.Cohorts.Where(c => years.Contains(c.Year)).ToList();
        // An ambassador sees colleagues' names on a shared year group, but only their own activity detail.
        var ambassadors = all ? r.Ambassadors : r.Ambassadors.Where(a => a.StaffId == admin.Id).ToList();
        var withMembers = cohorts.Where(c => c.ActiveMembers >= new HealthScoreConfig().MinimumCohortMembers).ToList();
        return new CohortsDto(days, cohorts, ambassadors, withMembers.Count, withMembers.Count(c => c.Ambassadors.Count > 0));
    }

    public async Task<IApiResponse<MonthlyReportDto>> GetMonthlyReportAsync(AuthData admin, string? month, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true)
    {
        try
        {
            var now = DateTime.UtcNow;
            var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var start = thisMonth;
            if (!string.IsNullOrWhiteSpace(month))
            {
                if (!DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
                    return ApiResponseExtensions.ToBadRequestApiResponse<MonthlyReportDto>("Month must look like 2026-09");
                start = new DateTime(parsed.Year, parsed.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                if (start > thisMonth) return ApiResponseExtensions.ToBadRequestApiResponse<MonthlyReportDto>("That month has not happened yet");
                if (start < thisMonth.AddMonths(-36)) return ApiResponseExtensions.ToBadRequestApiResponse<MonthlyReportDto>("Reports go back three years");
            }

            var shared = await GetSharedAsync(30, disabledFeatures.ToHashSet(), usesYearGroups, now);
            var current = await engine.ComputeMonthAsync(start);
            var previous = await engine.ComputeMonthAsync(start.AddMonths(-1));
            var next = await recommendationRepo.GetQueryable(r => r.Status == RecommendationStatuses.Open)
                .OrderBy(r => r.Priority == RecommendationPriorities.High ? 0 : r.Priority == RecommendationPriorities.Medium ? 1 : 2).ThenByDescending(r => r.CreatedAt).Take(5).ToListAsync();
            var weakest = shared.Health.Factors.Where(f => f.Score is < 60).OrderBy(f => f.Score).Take(3).ToList();

            return new MonthlyReportDto(start.ToString("yyyy-MM"), start == thisMonth, current, previous,
                new HealthDto(shared.Health.Score, shared.PreviousScore, shared.Health.Classification, shared.Health.Summary, shared.Health.Factors),
                weakest, next.Select(RecommendationDto.From).ToList(), shared.Cohorts, shared.Ambassadors).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Monthly report failed for {InstitutionId}", currentTenant.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<MonthlyReportDto>("Failed to build the monthly report");
        }
    }

    public async Task<IApiResponse<CohortsDto>> GetCohortsAsync(AuthData admin, int days, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true)
    {
        try
        {
            if (!AllowedPeriods.Contains(days)) days = 30;
            var shared = await GetSharedAsync(days, disabledFeatures.ToHashSet(), usesYearGroups, DateTime.UtcNow);
            return ScopeCohorts(shared, admin, days).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Cohorts failed for {InstitutionId}", currentTenant.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<CohortsDto>("Failed to load year groups");
        }
    }

    public async Task<IApiResponse<AmbassadorWorkspaceDto>> GetAmbassadorWorkspaceAsync(AuthData admin, int days, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true)
    {
        try
        {
            if (!AllowedPeriods.Contains(days)) days = 30;
            var shared = await GetSharedAsync(days, disabledFeatures.ToHashSet(), usesYearGroups, DateTime.UtcNow);
            var years = (admin.YearGroups ?? []).ToList();
            var recent = years.Count == 0 ? [] : await memberRepo.GetQueryable(m => years.Contains(m.GraduationYear) && (m.Status == "Active" || m.Status == "Pending"))
                .OrderByDescending(m => m.CreatedAt).Take(10)
                .Select(m => new RecentMember(m.FirstName + " " + m.LastName, m.GraduationYear, m.CreatedAt,
                    m.Bio == null && m.JobTitle == null && m.Company == null && m.Location == null && m.ProfilePictureUrl == null)).ToListAsync();
            var tasks = await recommendationRepo.GetQueryable(r => r.AssignedToId == admin.Id && r.Status == RecommendationStatuses.Open)
                .OrderByDescending(r => r.CreatedAt).Take(20).ToListAsync();
            return new AmbassadorWorkspaceDto(admin.Name, years.Order().ToList(), ScopeCohorts(shared, admin, days), recent, tasks.Select(RecommendationDto.From).ToList()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Ambassador workspace failed for {StaffId}", admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<AmbassadorWorkspaceDto>("Failed to load your workspace");
        }
    }

    public async Task<IApiResponse<RecommendationDto>> UpdateMyTaskAsync(AuthData admin, string id, string action)
    {
        try
        {
            // Tenant-filtered, and further limited to what was delegated to this person: they cannot touch anyone else's.
            var r = await recommendationRepo.GetOneAsync(x => x.Id == id && x.AssignedToId == admin.Id);
            if (r is null) return ApiResponseExtensions.ToNotFoundApiResponse<RecommendationDto>("That task was not found");
            var now = DateTime.UtcNow;
            switch (action.ToLowerInvariant())
            {
                case "complete": r.Status = RecommendationStatuses.Completed; r.ResolvedAt = now; r.ResolvedById = admin.Id; r.ResolvedByName = admin.Name; break;
                case "snooze": r.Status = RecommendationStatuses.Snoozed; r.SnoozedUntil = now.AddDays(3); break;
                default: return ApiResponseExtensions.ToBadRequestApiResponse<RecommendationDto>("Action must be complete or snooze");
            }
            r.UpdatedAt = now; r.UpdatedBy = admin.Id;
            await recommendationRepo.UpdateAsync(r);
            return RecommendationDto.From(r).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Updating task {Id} failed", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<RecommendationDto>("Failed to update the task");
        }
    }

    // The cached shape. A plain class so the cache can serialize it.
    private sealed record SharedReading(EngagementMetrics Metrics, HealthResult Health, int? PreviousScore, IReadOnlyList<CohortRow> Cohorts, IReadOnlyList<AmbassadorRow> Ambassadors);

    public async Task<IApiResponse<IReadOnlyList<HealthSnapshotDto>>> GetHealthHistoryAsync(int days, int limit)
    {
        try
        {
            if (!AllowedPeriods.Contains(days)) days = 30;
            limit = Math.Clamp(limit, 1, 180);
            var rows = await snapshotRepo.GetQueryable(s => s.PeriodDays == days).OrderByDescending(s => s.SnapshotDate).Take(limit).ToListAsync();
            return ((IReadOnlyList<HealthSnapshotDto>)rows.OrderBy(s => s.SnapshotDate)
                .Select(s => new HealthSnapshotDto(s.SnapshotDate, s.Score, s.Classification, s.ActiveMembers, s.Factors)).ToList()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Health history failed for {InstitutionId}", currentTenant.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<IReadOnlyList<HealthSnapshotDto>>("Failed to load health history");
        }
    }

    public async Task<IApiResponse<PgPagedResult<RecommendationDto>>> ListRecommendationsAsync(string? status, int page, int pageSize)
    {
        try
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            var result = await recommendationRepo.GetPagedAsync(Math.Max(page, 1), pageSize, "CreatedAt", "desc",
                string.IsNullOrWhiteSpace(status) ? null : r => r.Status == status);
            return new PgPagedResult<RecommendationDto>
            {
                PageIndex = result.PageIndex, PageSize = result.PageSize, Count = result.Count, TotalCount = result.TotalCount, TotalPages = result.TotalPages,
                Results = result.Results.Select(RecommendationDto.From).ToList(),
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Listing recommendations failed for {InstitutionId}", currentTenant.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<RecommendationDto>>("Failed to load recommendations");
        }
    }

    public async Task<IApiResponse<RecommendationDto>> ResolveRecommendationAsync(AuthData admin, string id, string action, int? snoozeDays)
    {
        try
        {
            var r = await recommendationRepo.GetOneAsync(x => x.Id == id);
            if (r is null) return ApiResponseExtensions.ToNotFoundApiResponse<RecommendationDto>("That recommendation was not found");
            var now = DateTime.UtcNow;
            switch (action.ToLowerInvariant())
            {
                case "complete": r.Status = RecommendationStatuses.Completed; r.ResolvedAt = now; r.ResolvedById = admin.Id; r.ResolvedByName = admin.Name; break;
                case "dismiss": r.Status = RecommendationStatuses.Dismissed; r.ResolvedAt = now; r.ResolvedById = admin.Id; r.ResolvedByName = admin.Name; break;
                case "snooze":
                    var d = Math.Clamp(snoozeDays ?? 3, 1, 30);
                    r.Status = RecommendationStatuses.Snoozed; r.SnoozedUntil = now.AddDays(d); break;
                default: return ApiResponseExtensions.ToBadRequestApiResponse<RecommendationDto>("Action must be complete, dismiss or snooze");
            }
            r.UpdatedAt = now; r.UpdatedBy = admin.Id;
            await recommendationRepo.UpdateAsync(r);
            return RecommendationDto.From(r).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Resolving recommendation {Id} failed", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<RecommendationDto>("Failed to update the recommendation");
        }
    }

    public async Task<IApiResponse<RecommendationDto>> AssignRecommendationAsync(AuthData admin, string id, string? staffId)
    {
        try
        {
            var r = await recommendationRepo.GetOneAsync(x => x.Id == id);
            if (r is null) return ApiResponseExtensions.ToNotFoundApiResponse<RecommendationDto>("That recommendation was not found");
            if (string.IsNullOrWhiteSpace(staffId)) { r.AssignedToId = null; r.AssignedToName = null; }
            else
            {
                // Looked up through the tenant-filtered repository: someone from another institution can never be assigned.
                var staff = await staffRepo.GetOneAsync(s => s.Id == staffId);
                if (staff is null) return ApiResponseExtensions.ToBadRequestApiResponse<RecommendationDto>("That administrator was not found");
                r.AssignedToId = staff.Id; r.AssignedToName = $"{staff.FirstName} {staff.LastName}".Trim();
            }
            r.UpdatedAt = DateTime.UtcNow; r.UpdatedBy = admin.Id;
            await recommendationRepo.UpdateAsync(r);
            return RecommendationDto.From(r).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Assigning recommendation {Id} failed", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<RecommendationDto>("Failed to assign the recommendation");
        }
    }

    public async Task<IApiResponse<object>> SetChecklistItemAsync(AuthData admin, string itemKey, bool done)
    {
        try
        {
            var week = EngagementChecklist.WeekStart(DateTime.UtcNow);
            var existing = await checklistRepo.GetOneAsync(e => e.WeekStart == week && e.ItemKey == itemKey);
            if (done && existing is null)
                await checklistRepo.AddAsync(new EngagementChecklistEntry
                {
                    InstitutionId = currentTenant.InstitutionId!, WeekStart = week, ItemKey = itemKey.Trim()[..Math.Min(40, itemKey.Trim().Length)],
                    CompletedById = admin.Id, CompletedByName = admin.Name,
                });
            else if (!done && existing is not null)
                await checklistRepo.RemoveAsync(existing);
            return ((object)new { itemKey, done }).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Checklist update failed for {ItemKey}", itemKey);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to update the checklist");
        }
    }
}
