using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Cross-institution analytics. Every query here ignores the tenant filter on purpose — platform staff
/// belong to no institution — and aggregates in the database, grouped by institution or by month, so
/// the cost depends on how many institutions there are, not on how many members or payments.
/// </summary>
public class PlatformAnalyticsService(
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<StoreOrder> storeOrderRepo,
    IAlumniPgRepository<ServiceRequest> serviceRequestRepo,
    ILogger<PlatformAnalyticsService> logger) : IPlatformAnalyticsService
{
    private const int TrendMonths = 12;
    private const int ListSize = 8;

    private record Payments(string InstitutionId, int Year, int Month, decimal Collected, decimal Earned, DateTime Latest);

    public async Task<IApiResponse<PlatformAnalyticsDto>> GetAnalyticsAsync(bool includeMoney)
    {
        try
        {
            var now = DateTime.UtcNow;
            var last30 = now.AddDays(-30);
            var last90 = now.AddDays(-90);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var trendStart = monthStart.AddMonths(-(TrendMonths - 1));
            var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastYearStart = yearStart.AddYears(-1);
            var lastYearToday = now.AddYears(-1);

            var institutions = await institutionRepo.GetQueryable(null, ignoreQueryFilters: true)
                .Select(i => new { i.Id, i.Name, i.OnboardedAt, i.ActivatedAt }).ToListAsync();

            var membersByInstitution = await memberRepo.GetQueryable(null, ignoreQueryFilters: true)
                .GroupBy(m => m.InstitutionId)
                .Select(g => new
                {
                    InstitutionId = g.Key,
                    Total = g.Count(),
                    Approved = g.Count(m => m.Status == "Active"),
                    Recent = g.Count(m => m.LastLoginAt >= last30),
                    LastSignIn = g.Max(m => m.LastLoginAt),
                }).ToListAsync();
            var memberLookup = membersByInstitution.ToDictionary(m => m.InstitutionId);

            var joinedByMonth = await memberRepo.GetQueryable(m => m.CreatedAt >= trendStart, ignoreQueryFilters: true)
                .GroupBy(m => new { m.CreatedAt.Year, m.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync();

            // Successful payments from all three sources, per institution per month, all time. One row per
            // institution-month-source, so this stays small however many payments there are.
            var payments = new List<Payments>();
            payments.AddRange((await contributionRepo.GetQueryable(c => c.Status == "Successful", ignoreQueryFilters: true)
                .GroupBy(c => new { c.InstitutionId, (c.ConfirmedAt ?? c.CreatedAt).Year, (c.ConfirmedAt ?? c.CreatedAt).Month })
                .Select(g => new { g.Key.InstitutionId, g.Key.Year, g.Key.Month, Collected = g.Sum(c => c.Amount), Earned = g.Sum(c => c.PlatformRevenueAmount), Latest = g.Max(c => c.ConfirmedAt ?? c.CreatedAt) })
                .ToListAsync()).Select(x => new Payments(x.InstitutionId, x.Year, x.Month, x.Collected, x.Earned, x.Latest)));
            payments.AddRange((await storeOrderRepo.GetQueryable(o => o.Status == "Successful", ignoreQueryFilters: true)
                .GroupBy(o => new { o.InstitutionId, (o.ConfirmedAt ?? o.CreatedAt).Year, (o.ConfirmedAt ?? o.CreatedAt).Month })
                .Select(g => new { g.Key.InstitutionId, g.Key.Year, g.Key.Month, Collected = g.Sum(o => o.TotalAmount), Earned = g.Sum(o => o.PlatformFeeAmount), Latest = g.Max(o => o.ConfirmedAt ?? o.CreatedAt) })
                .ToListAsync()).Select(x => new Payments(x.InstitutionId, x.Year, x.Month, x.Collected, x.Earned, x.Latest)));
            payments.AddRange((await serviceRequestRepo.GetQueryable(r => r.PaymentStatus == "Successful", ignoreQueryFilters: true)
                .GroupBy(r => new { r.InstitutionId, (r.ConfirmedAt ?? r.CreatedAt).Year, (r.ConfirmedAt ?? r.CreatedAt).Month })
                .Select(g => new { g.Key.InstitutionId, g.Key.Year, g.Key.Month, Collected = g.Sum(r => r.Amount), Earned = g.Sum(r => r.PlatformFeeAmount), Latest = g.Max(r => r.ConfirmedAt ?? r.CreatedAt) })
                .ToListAsync()).Select(x => new Payments(x.InstitutionId, x.Year, x.Month, x.Collected, x.Earned, x.Latest)));

            var lastPayment = payments.GroupBy(p => p.InstitutionId).ToDictionary(g => g.Key, g => g.Max(p => p.Latest));
            // "Last 90 days" for the leaderboard needs day precision, which the monthly rollup above doesn't have.
            var recentPayments = new Dictionary<string, (decimal Collected, decimal Earned)>();
            void AddRecent(IEnumerable<(string InstitutionId, decimal Collected, decimal Earned)> rows)
            {
                foreach (var (id, collected, earned) in rows)
                {
                    var current = recentPayments.GetValueOrDefault(id);
                    recentPayments[id] = (current.Collected + collected, current.Earned + earned);
                }
            }
            AddRecent((await contributionRepo.GetQueryable(c => c.Status == "Successful" && (c.ConfirmedAt ?? c.CreatedAt) >= last90, ignoreQueryFilters: true)
                .GroupBy(c => c.InstitutionId).Select(g => new { g.Key, Collected = g.Sum(c => c.Amount), Earned = g.Sum(c => c.PlatformRevenueAmount) })
                .ToListAsync()).Select(x => (x.Key, x.Collected, x.Earned)));
            AddRecent((await storeOrderRepo.GetQueryable(o => o.Status == "Successful" && (o.ConfirmedAt ?? o.CreatedAt) >= last90, ignoreQueryFilters: true)
                .GroupBy(o => o.InstitutionId).Select(g => new { g.Key, Collected = g.Sum(o => o.TotalAmount), Earned = g.Sum(o => o.PlatformFeeAmount) })
                .ToListAsync()).Select(x => (x.Key, x.Collected, x.Earned)));
            AddRecent((await serviceRequestRepo.GetQueryable(r => r.PaymentStatus == "Successful" && (r.ConfirmedAt ?? r.CreatedAt) >= last90, ignoreQueryFilters: true)
                .GroupBy(r => r.InstitutionId).Select(g => new { g.Key, Collected = g.Sum(r => r.Amount), Earned = g.Sum(r => r.PlatformFeeAmount) })
                .ToListAsync()).Select(x => (x.Key, x.Collected, x.Earned)));

            var institutionIds = institutions.Select(i => i.Id).ToHashSet();
            var summary = new PlatformAnalyticsInstitutionsDto(
                institutions.Count,
                institutions.Count(i => i.ActivatedAt != null),
                recentPayments.Keys.Count(institutionIds.Contains),
                membersByInstitution.Count(m => m.Recent > 0 && institutionIds.Contains(m.InstitutionId)));
            var members = new PlatformAnalyticsMembersDto(
                membersByInstitution.Sum(m => m.Total), membersByInstitution.Sum(m => m.Approved), membersByInstitution.Sum(m => m.Recent));

            var runningInstitutions = institutions.Count(i => i.OnboardedAt < trendStart);
            var institutionGrowth = Months(trendStart).Select(m =>
            {
                var count = institutions.Count(i => i.OnboardedAt.Year == m.Year && i.OnboardedAt.Month == m.Month);
                runningInstitutions += count;
                return new PlatformAnalyticsMonthCountDto(m.Year, m.Month, count, runningInstitutions);
            }).ToList();

            var runningMembers = members.Total - joinedByMonth.Sum(j => j.Count);
            var memberGrowth = Months(trendStart).Select(m =>
            {
                var count = joinedByMonth.FirstOrDefault(j => j.Year == m.Year && j.Month == m.Month)?.Count ?? 0;
                runningMembers += count;
                return new PlatformAnalyticsMonthCountDto(m.Year, m.Month, count, runningMembers);
            }).ToList();

            PlatformAnalyticsMoneyDto? money = null;
            List<PlatformAnalyticsLeaderDto> leaders = [];
            if (includeMoney)
            {
                // Like-for-like needs last year cut at today's date, which the monthly rollup can't give for the current month.
                var lastYearToDate = await SumBetweenAsync(lastYearStart, lastYearToday);
                money = new PlatformAnalyticsMoneyDto(
                    payments.Where(p => p.Year == now.Year).Sum(p => p.Collected), lastYearToDate.Collected,
                    payments.Where(p => p.Year == now.Year).Sum(p => p.Earned), lastYearToDate.Earned,
                    Months(trendStart).Select(m => new PlatformAnalyticsMoneyMonthDto(m.Year, m.Month,
                        payments.Where(p => p.Year == m.Year && p.Month == m.Month).Sum(p => p.Collected),
                        payments.Where(p => p.Year == m.Year && p.Month == m.Month).Sum(p => p.Earned))).ToList());

                leaders = institutions.Where(i => recentPayments.ContainsKey(i.Id))
                    .Select(i => new PlatformAnalyticsLeaderDto(i.Id, i.Name, recentPayments[i.Id].Collected, recentPayments[i.Id].Earned, memberLookup.GetValueOrDefault(i.Id)?.Total ?? 0))
                    .OrderByDescending(l => l.Collected).ThenBy(l => l.Name).Take(ListSize).ToList();
            }

            // Institutions that have members but where none has signed in for 30 days: the ones longest silent first.
            var quiet = institutions
                .Select(i => new { i.Id, i.Name, Members = memberLookup.GetValueOrDefault(i.Id) })
                .Where(x => x.Members is { Total: > 0, Recent: 0 })
                .Select(x => new PlatformAnalyticsQuietDto(x.Id, x.Name, x.Members!.Total, x.Members.LastSignIn, lastPayment.TryGetValue(x.Id, out var paid) ? paid : null))
                .OrderBy(q => q.LastSignIn ?? DateTime.MinValue).ThenBy(q => q.Name).Take(ListSize).ToList();

            return new PlatformAnalyticsDto(summary, members, institutionGrowth, memberGrowth, money, leaders, quiet).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to build platform analytics");
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformAnalyticsDto>("Failed to retrieve analytics");
        }
    }

    private async Task<(decimal Collected, decimal Earned)> SumBetweenAsync(DateTime from, DateTime to)
    {
        var contributions = await contributionRepo.GetQueryable(c => c.Status == "Successful" && (c.ConfirmedAt ?? c.CreatedAt) >= from && (c.ConfirmedAt ?? c.CreatedAt) <= to, ignoreQueryFilters: true)
            .GroupBy(c => 1).Select(g => new { Collected = g.Sum(c => c.Amount), Earned = g.Sum(c => c.PlatformRevenueAmount) }).FirstOrDefaultAsync();
        var orders = await storeOrderRepo.GetQueryable(o => o.Status == "Successful" && (o.ConfirmedAt ?? o.CreatedAt) >= from && (o.ConfirmedAt ?? o.CreatedAt) <= to, ignoreQueryFilters: true)
            .GroupBy(o => 1).Select(g => new { Collected = g.Sum(o => o.TotalAmount), Earned = g.Sum(o => o.PlatformFeeAmount) }).FirstOrDefaultAsync();
        var requests = await serviceRequestRepo.GetQueryable(r => r.PaymentStatus == "Successful" && (r.ConfirmedAt ?? r.CreatedAt) >= from && (r.ConfirmedAt ?? r.CreatedAt) <= to, ignoreQueryFilters: true)
            .GroupBy(r => 1).Select(g => new { Collected = g.Sum(r => r.Amount), Earned = g.Sum(r => r.PlatformFeeAmount) }).FirstOrDefaultAsync();
        return ((contributions?.Collected ?? 0) + (orders?.Collected ?? 0) + (requests?.Collected ?? 0),
                (contributions?.Earned ?? 0) + (orders?.Earned ?? 0) + (requests?.Earned ?? 0));
    }

    private static IEnumerable<DateTime> Months(DateTime first) => Enumerable.Range(0, TrendMonths).Select(i => first.AddMonths(i));
}
