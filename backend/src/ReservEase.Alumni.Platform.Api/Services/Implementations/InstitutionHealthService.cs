using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// The platform's view of how institutions are doing. Reads each institution's recorded health readings (written daily by the
/// worker), counts, and administrator activity, across every institution. It never reads a member, a payment or a message:
/// platform staff see aggregates only, which is also why every query here is a count, a maximum or a stored reading.
/// </summary>
public class InstitutionHealthService(
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<CommunityHealthSnapshot> snapshotRepo,
    IAlumniPgRepository<EngagementRecommendation> recommendationRepo,
    IAlumniPgRepository<StaffEntity> staffRepo,
    IAlumniPgRepository<StaffActivityWeek> activityRepo,
    IAlumniPgRepository<ActivationTask> taskRepo,
    ILogger<InstitutionHealthService> logger) : IInstitutionHealthService
{
    public async Task<IApiResponse<InstitutionHealthDto>> GetAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var institutions = await institutionRepo.GetQueryable(i => i.Status == "Active", ignoreQueryFilters: true)
                .Select(i => new { i.Id, i.Name, i.Slug, i.CreatedAt }).ToListAsync();

            var readings = await snapshotRepo.GetQueryable(s => s.PeriodDays == 30 && s.SnapshotDate >= today.AddDays(-45), ignoreQueryFilters: true)
                .Select(s => new { s.InstitutionId, s.SnapshotDate, s.Score, s.Classification, s.ActiveMembers }).ToListAsync();
            var open = (await recommendationRepo.GetQueryable(r => r.Status == RecommendationStatuses.Open, ignoreQueryFilters: true)
                .GroupBy(r => r.InstitutionId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync()).ToDictionary(x => x.Id, x => x.Count);

            var admins = await staffRepo.GetQueryable(s => !s.IsDisabled, ignoreQueryFilters: true)
                .Select(s => new { s.Id, s.InstitutionId, s.Role, s.LastLoginAt, HasYears = s.YearGroups != null && s.YearGroups.Count > 0 }).ToListAsync();
            var weeks = (await activityRepo.GetQueryable(w => w.WeekStart >= today.AddDays(-60))
                .GroupBy(w => w.StaffId).Select(g => new { Id = g.Key, Last = g.Max(w => w.WeekStart) }).ToListAsync()).ToDictionary(x => x.Id, x => x.Last);

            var tasks = await taskRepo.GetQueryable(t => t.InstitutionId != null && t.Status != TaskStatuses.Done)
                .Select(t => new { t.InstitutionId, t.DueDate }).ToListAsync();

            var rows = new List<InstitutionHealthRow>();
            foreach (var i in institutions)
            {
                var mine = readings.Where(r => r.InstitutionId == i.Id).OrderByDescending(r => r.SnapshotDate).ToList();
                var latest = mine.FirstOrDefault();
                var weekAgo = latest is null ? null : mine.FirstOrDefault(r => r.SnapshotDate <= latest.SnapshotDate.AddDays(-7) && r.Score != null);
                int? change = latest?.Score is { } s && weekAgo?.Score is { } w ? s - w : null;

                var staff = admins.Where(a => a.InstitutionId == i.Id).ToList();
                var lastAdmin = staff.Where(a => a.Role == "SuperAdmin")
                    .Select(a => new[] { a.LastLoginAt, weeks.TryGetValue(a.Id, out var wk) ? (DateTime?)wk.AddDays(6) : null }.Max())
                    .Where(d => d is not null).Max();
                int? daysAway = lastAdmin is { } la ? Math.Max(0, (int)(now - la).TotalDays) : null;

                var followUps = tasks.Where(t => t.InstitutionId == i.Id).ToList();
                var overdue = followUps.Count(t => t.DueDate is { } d && d.Date < today);
                var facts = new InstitutionHealthFacts(i.CreatedAt, now, latest?.Classification, latest?.Score, weekAgo?.Score, latest?.ActiveMembers ?? 0,
                    daysAway, open.GetValueOrDefault(i.Id), overdue);
                var (status, reasons) = InstitutionHealthAssessment.Assess(facts);

                rows.Add(new InstitutionHealthRow(i.Id, i.Name, i.Slug, i.CreatedAt, status, reasons, latest?.Classification, latest?.Score, change,
                    latest?.ActiveMembers ?? 0, daysAway, open.GetValueOrDefault(i.Id), staff.Count(a => a.Role == "ScopedAdmin" && a.HasYears), followUps.Count, overdue));
            }

            var ordered = rows.OrderBy(r => r.Status == InstitutionHealthStatuses.NeedsAttention ? 0 : r.Status == InstitutionHealthStatuses.Onboarding ? 1 : 2)
                .ThenBy(r => r.Score ?? 101).ThenBy(r => r.Name).ToList();
            var scored = rows.Where(r => r.Score.HasValue).ToList();
            var summary = new InstitutionHealthSummary(
                rows.Count, rows.Count(r => r.Status == InstitutionHealthStatuses.NeedsAttention), rows.Count(r => r.Status == InstitutionHealthStatuses.OnTrack),
                rows.Count(r => r.Status == InstitutionHealthStatuses.Onboarding), rows.Count(r => r.Classification == "Healthy"),
                rows.Count(r => r.Classification is "AtRisk" or "Inactive"), rows.Count(r => r.Classification is null),
                rows.Count(r => r.Ambassadors > 0), scored.Count == 0 ? null : Math.Round(scored.Average(r => (double)r.Score!.Value), 1));
            return new InstitutionHealthDto(summary, ordered).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Institution health overview failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionHealthDto>("Failed to load institution health");
        }
    }
}
