using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class AnalyticsService(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Department> departmentRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<StoreOrder> storeOrderRepo,
    IAlumniPgRepository<ServiceRequest> serviceRequestRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    IAlumniPgRepository<ForumPost> postRepo,
    ICurrentTenantService currentTenant,
    IRedisService<InstitutionRedisConfig> cache,
    ILogger<AnalyticsService> logger) : IAnalyticsService
{
    private const int TrendMonths = 12;
    private const int TopSlices = 8;

    public async Task<IApiResponse<InstitutionAnalyticsDto>> GetAnalyticsAsync(AuthData admin, IReadOnlyCollection<string> disabledFeatures)
    {
        try
        {
            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            // Same keying as the report summary: SuperAdmins share one entry, a ScopedAdmin's depends on their own scope.
            // Five minutes, not one: this is trend analysis over a dozen queries, and nothing on the page is a live counter.
            var cacheKey = $"analytics:{currentTenant.InstitutionId}:{(isSuper ? "super" : admin.Id)}:{string.Join(",", disabledFeatures.Order())}";
            var cached = await cache.GetAsync<InstitutionAnalyticsDto>(cacheKey);
            if (cached is not null) return cached.ToOkApiResponse();

            bool On(string feature) => !disabledFeatures.Contains(feature);
            var now = DateTime.UtcNow;
            var last30 = now.AddDays(-30);
            var previous30 = now.AddDays(-60);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var trendStart = monthStart.AddMonths(-(TrendMonths - 1));

            // ── Scope ────────────────────────────────────────────────────────
            var yearGroups = admin.YearGroups ?? [];
            var communityIds = admin.CommunityIds ?? [];
            var communityMemberIds = isSuper || communityIds.Count == 0
                ? []
                : await membershipRepo.GetQueryable(m => communityIds.Contains(m.CommunityId) && m.Status == "Approved")
                    .Select(m => m.MemberId).Distinct().ToListAsync();

            IQueryable<Member> Members() => isSuper
                ? memberRepo.GetQueryable()
                : memberRepo.GetQueryable(m => yearGroups.Contains(m.GraduationYear) || communityMemberIds.Contains(m.Id));
            IQueryable<Campaign> Campaigns() => isSuper
                ? campaignRepo.GetQueryable()
                : campaignRepo.GetQueryable(c => c.CreatedBy == admin.Id
                    || (c.YearGroups != null && c.YearGroups.Any(y => yearGroups.Contains(y)))
                    || (c.CommunityId != null && communityIds.Contains(c.CommunityId)));
            IQueryable<Contribution> Contributions()
            {
                if (isSuper) return contributionRepo.GetQueryable();
                var campaignIds = Campaigns().Select(c => c.Id);
                return contributionRepo.GetQueryable(c => campaignIds.Contains(c.CampaignId));
            }

            // ── Members and growth ───────────────────────────────────────────
            var memberCounts = await Members().GroupBy(m => 1).Select(g => new
            {
                Total = g.Count(),
                Approved = g.Count(m => m.Status == "Active"),
                Pending = g.Count(m => m.Status == "Pending"),
                SignedInEver = g.Count(m => m.Status == "Active" && m.LastLoginAt != null),
                SignedInRecently = g.Count(m => m.Status == "Active" && m.LastLoginAt >= last30),
            }).FirstOrDefaultAsync();
            var members = new AnalyticsMembersDto(memberCounts?.Total ?? 0, memberCounts?.Approved ?? 0, memberCounts?.Pending ?? 0,
                memberCounts?.SignedInEver ?? 0, memberCounts?.SignedInRecently ?? 0);

            var joinedByMonth = await Members().Where(m => m.CreatedAt >= trendStart)
                .GroupBy(m => new { m.CreatedAt.Year, m.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync();
            var running = members.Total - joinedByMonth.Sum(j => j.Count);
            var growth = Months(trendStart).Select(m =>
            {
                var count = joinedByMonth.FirstOrDefault(j => j.Year == m.Year && j.Month == m.Month)?.Count ?? 0;
                running += count;
                return new AnalyticsMonthCountDto(m.Year, m.Month, count, running);
            }).ToList();

            // ── Dues ─────────────────────────────────────────────────────────
            AnalyticsDuesDto? dues = null;
            if (On(InstitutionFeatures.Contributions))
            {
                var year = now.Year;
                var duesCampaigns = await Campaigns().Where(c => c.IsMembershipCampaign && c.MembershipYear == year)
                    .Select(c => new { c.Id, c.YearGroups }).ToListAsync();
                if (duesCampaigns.Count > 0)
                {
                    var duesCampaignIds = duesCampaigns.Select(c => c.Id).ToList();
                    var openToAll = duesCampaigns.Any(c => c.YearGroups is null || c.YearGroups.Count == 0);
                    var targetedYears = duesCampaigns.SelectMany(c => c.YearGroups ?? []).Distinct().ToList();

                    // Who the dues fall on: approved members from the year they graduate — the rule the
                    // member portal and the dues-standing report both use.
                    var eligibleMembers = Members().Where(m => m.Status == "Active" && m.GraduationYear <= year
                        && (openToAll || targetedYears.Contains(m.GraduationYear)));
                    var eligibleByYear = await eligibleMembers.GroupBy(m => m.GraduationYear)
                        .Select(g => new { YearGroup = g.Key, Count = g.Count() }).ToListAsync();

                    var payments = contributionRepo.GetQueryable(c => c.Status == "Successful" && duesCampaignIds.Contains(c.CampaignId));
                    var collected = await payments.SumAsync(c => (decimal?)c.Amount) ?? 0;
                    var payerIds = payments.Select(c => c.MemberId).Distinct();
                    var paidByYear = await eligibleMembers.Where(m => payerIds.Contains(m.Id)).GroupBy(m => m.GraduationYear)
                        .Select(g => new { YearGroup = g.Key, Count = g.Count() }).ToListAsync();

                    dues = new AnalyticsDuesDto(year, eligibleByYear.Sum(e => e.Count), paidByYear.Sum(p => p.Count), collected,
                        eligibleByYear.Where(e => e.YearGroup > 0).OrderBy(e => e.YearGroup)
                            .Select(e => new AnalyticsDuesYearGroupDto(e.YearGroup, e.Count, paidByYear.FirstOrDefault(p => p.YearGroup == e.YearGroup)?.Count ?? 0))
                            .ToList());
                }
            }

            // ── Money ────────────────────────────────────────────────────────
            AnalyticsMoneyDto? money = null;
            // Store and Services carry no year group or community, so, as everywhere else, only a SuperAdmin sees them.
            var contributionsOn = On(InstitutionFeatures.Contributions);
            var storeOn = isSuper && On(InstitutionFeatures.Store);
            var servicesOn = isSuper && On(InstitutionFeatures.Services);
            if (contributionsOn || storeOn || servicesOn)
            {
                var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var lastYearStart = yearStart.AddYears(-1);
                var lastYearToday = now.AddYears(-1);
                var earliest = trendStart < lastYearStart ? trendStart : lastYearStart;

                // One pass per source over everything since the earlier of the two windows; split into the trend and the two totals in memory.
                var contributionMonths = contributionsOn
                    ? await Contributions().Where(c => c.Status == "Successful" && (c.ConfirmedAt ?? c.CreatedAt) >= earliest)
                        .Select(c => new { At = c.ConfirmedAt ?? c.CreatedAt, c.Amount }).ToListAsync()
                    : [];
                var storeMonths = storeOn
                    ? await storeOrderRepo.GetQueryable(o => o.Status == "Successful" && (o.ConfirmedAt ?? o.CreatedAt) >= earliest)
                        .Select(o => new { At = o.ConfirmedAt ?? o.CreatedAt, Amount = o.TotalAmount }).ToListAsync()
                    : [];
                var serviceMonths = servicesOn
                    ? await serviceRequestRepo.GetQueryable(r => r.PaymentStatus == "Successful" && (r.ConfirmedAt ?? r.CreatedAt) >= earliest)
                        .Select(r => new { At = r.ConfirmedAt ?? r.CreatedAt, r.Amount }).ToListAsync()
                    : [];
                var all = contributionMonths.Concat(storeMonths).Concat(serviceMonths).ToList();

                var payers = contributionsOn
                    ? await Contributions().Where(c => c.Status == "Successful" && (c.ConfirmedAt ?? c.CreatedAt) >= yearStart)
                        .Select(c => c.MemberId).Distinct().CountAsync()
                    : 0;

                money = new AnalyticsMoneyDto(
                    all.Where(p => p.At >= yearStart).Sum(p => p.Amount),
                    all.Where(p => p.At >= lastYearStart && p.At <= lastYearToday).Sum(p => p.Amount),
                    payers,
                    Months(trendStart).Select(m => new AnalyticsMoneyMonthDto(m.Year, m.Month,
                        contributionMonths.Where(p => p.At.Year == m.Year && p.At.Month == m.Month).Sum(p => p.Amount),
                        storeMonths.Where(p => p.At.Year == m.Year && p.At.Month == m.Month).Sum(p => p.Amount),
                        serviceMonths.Where(p => p.At.Year == m.Year && p.At.Month == m.Month).Sum(p => p.Amount))).ToList());
            }

            // ── Composition ──────────────────────────────────────────────────
            var approved = Members().Where(m => m.Status == "Active");
            var byYear = await approved.Where(m => m.GraduationYear > 0).GroupBy(m => m.GraduationYear)
                .Select(g => new { Year = g.Key, Count = g.Count() }).ToListAsync();
            var byDepartment = await approved.GroupBy(m => m.DepartmentId)
                .Select(g => new { DepartmentId = g.Key, Count = g.Count() }).ToListAsync();
            var departmentNames = (await departmentRepo.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            // Location is free text, so "Accra", "accra " and "ACCRA" are folded together; the commonest spelling is the label.
            var locations = (await approved.Where(m => m.Location != null && m.Location != "").GroupBy(m => m.Location!)
                    .Select(g => new { Location = g.Key, Count = g.Count() }).ToListAsync())
                .GroupBy(l => l.Location.Trim().ToLowerInvariant())
                .Where(g => g.Key.Length > 0)
                .Select(g => new AnalyticsSliceDto(g.OrderByDescending(l => l.Count).First().Location.Trim(), g.Sum(l => l.Count)))
                .OrderByDescending(s => s.Count).ThenBy(s => s.Label).ToList();

            var composition = new AnalyticsCompositionDto(
                byYear.OrderBy(y => y.Year).Select(y => new AnalyticsSliceDto(y.Year.ToString(), y.Count)).ToList(),
                byDepartment.Where(d => departmentNames.ContainsKey(d.DepartmentId))
                    .Select(d => new AnalyticsSliceDto(departmentNames[d.DepartmentId], d.Count))
                    .OrderByDescending(s => s.Count).ThenBy(s => s.Label).Take(TopSlices).ToList(),
                locations.Take(TopSlices).ToList(),
                members.Approved - locations.Sum(l => l.Count));

            // ── Last 30 days ─────────────────────────────────────────────────
            var memberIds = Members().Select(m => m.Id);
            async Task<AnalyticsChangeDto> Change<T>(IQueryable<T> rows, System.Linq.Expressions.Expression<Func<T, bool>> recent, System.Linq.Expressions.Expression<Func<T, bool>> before) =>
                new(await rows.CountAsync(recent), await rows.CountAsync(before));

            var activity = new AnalyticsActivityDto(
                await Change(Members(), m => m.CreatedAt >= last30, m => m.CreatedAt >= previous30 && m.CreatedAt < last30),
                contributionsOn
                    ? await Change(Contributions().Where(c => c.Status == "Successful"),
                        c => (c.ConfirmedAt ?? c.CreatedAt) >= last30, c => (c.ConfirmedAt ?? c.CreatedAt) >= previous30 && (c.ConfirmedAt ?? c.CreatedAt) < last30)
                    : new(0, 0),
                On(InstitutionFeatures.Events)
                    ? await Change(rsvpRepo.GetQueryable(r => r.Status == "Confirmed" && (isSuper || memberIds.Contains(r.MemberId))),
                        r => r.CreatedAt >= last30, r => r.CreatedAt >= previous30 && r.CreatedAt < last30)
                    : new(0, 0),
                On(InstitutionFeatures.Forum)
                    ? await ForumActivityAsync(isSuper, memberIds, last30, previous30)
                    : new(0, 0));

            var result = new InstitutionAnalyticsDto(members, growth, dues, money, composition, activity);
            await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
            return result.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to build analytics");
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionAnalyticsDto>("Failed to retrieve analytics");
        }
    }

    /// <summary>New threads and replies together: both are a member saying something in the forum.</summary>
    private async Task<AnalyticsChangeDto> ForumActivityAsync(bool isSuper, IQueryable<string> memberIds, DateTime last30, DateTime previous30)
    {
        var threads = threadRepo.GetQueryable(t => isSuper || memberIds.Contains(t.AuthorId));
        var posts = postRepo.GetQueryable(p => !p.IsDeleted && (isSuper || memberIds.Contains(p.AuthorId)));
        return new AnalyticsChangeDto(
            await threads.CountAsync(t => t.CreatedAt >= last30) + await posts.CountAsync(p => p.CreatedAt >= last30),
            await threads.CountAsync(t => t.CreatedAt >= previous30 && t.CreatedAt < last30) + await posts.CountAsync(p => p.CreatedAt >= previous30 && p.CreatedAt < last30));
    }

    /// <summary>The trend's calendar months, oldest first, this month last — so a month in which nothing happened still has its place on the chart.</summary>
    private static IEnumerable<DateTime> Months(DateTime first) => Enumerable.Range(0, TrendMonths).Select(i => first.AddMonths(i));
}
