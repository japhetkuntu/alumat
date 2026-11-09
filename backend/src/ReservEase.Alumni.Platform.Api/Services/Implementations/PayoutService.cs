using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;
using StoreOrderEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.StoreOrder;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using EventRsvpEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.EventRsvp;
using ServiceRequestEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.ServiceRequest;
using CampaignEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Campaign;
using BatchEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Batch;
using CommunityEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Community;
using ServiceTypeEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.ServiceType;
using EventEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.AlumniEvent;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Cross-tenant mirror of Institution.Api's own PayoutService — same
/// <see cref="PayoutWindowCalculator"/> windows, same successful+Paystack
/// filter, same sum, just grouped by institution instead of scoped to one.
/// Deliberately duplicated rather than shared as a service (Platform.Api has
/// no tenant context to inject), but every institution's figures computed
/// here are guaranteed to equal what that institution's own SuperAdmins see,
/// since both start from PayoutWindowCalculator and the same raw formula
/// (including the same batch-settlement exclusion — see ExcludeBatchSettledAsync).
/// </summary>
public class PayoutService(
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<ContributionEntity> contributionRepo,
    IAlumniPgRepository<StoreOrderEntity> storeOrderRepo,
    IAlumniPgRepository<ServiceRequestEntity> serviceRequestRepo,
    IAlumniPgRepository<EventRsvpEntity> eventRsvpRepo,
    IAlumniPgRepository<CampaignEntity> campaignRepo,
    IAlumniPgRepository<BatchEntity> batchRepo,
    IAlumniPgRepository<CommunityEntity> communityRepo,
    IAlumniPgRepository<EventEntity> eventRepo,
    IAlumniPgRepository<ServiceTypeEntity> serviceTypeRepo,
    ILogger<PayoutService> logger) : IPayoutService
{
    public async Task<IApiResponse<PlatformPayoutForecastResponse>> GetForecastAsync()
    {
        try
        {
        var windows = PayoutWindowCalculator.GetWindows(DateTime.UtcNow);

        var institutions = await institutionRepo.GetQueryable(ignoreQueryFilters: true)
            .Select(i => new { i.Id, i.Name, i.PaystackSubaccountCode })
            .ToListAsync();

        // Each window is read once in full; the part that settles into the institution's own account is what is left after taking out
        // what goes into a group's or year group's own account, and the part taken out is shown beside it.
        var lastContribsAll = await FetchContributionsAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var nextContribsAll = await FetchContributionsAsync(windows.NextWindowStart, windows.NextWindowEnd);
        var lastRequestsAll = await FetchServiceRequestsAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var nextRequestsAll = await FetchServiceRequestsAsync(windows.NextWindowStart, windows.NextWindowEnd);
        var lastTicketsAll = await FetchTicketsAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var nextTicketsAll = await FetchTicketsAsync(windows.NextWindowStart, windows.NextWindowEnd);

        var lastContribs = await ExcludeBatchSettledAsync(lastContribsAll);
        var lastOrders = await FetchOrdersAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var lastRequests = await ExcludeAccountSettledRequestsAsync(lastRequestsAll);
        var lastTickets = await ExcludeGroupSettledTicketsAsync(lastTicketsAll);
        var nextContribs = await ExcludeBatchSettledAsync(nextContribsAll);
        var nextOrders = await FetchOrdersAsync(windows.NextWindowStart, windows.NextWindowEnd);
        var nextRequests = await ExcludeAccountSettledRequestsAsync(nextRequestsAll);
        var nextTickets = await ExcludeGroupSettledTicketsAsync(nextTicketsAll);

        // What went elsewhere, per institution: everything read, less what stayed with the institution's own account.
        static Dictionary<string, (decimal Amount, int Count)> Elsewhere<T>(List<T> all, List<T> kept, Func<T, string> institution, Func<T, decimal> amount) where T : class
        {
            var stayed = kept.ToHashSet();
            return all.Where(x => !stayed.Contains(x)).GroupBy(institution).ToDictionary(g => g.Key, g => (g.Sum(amount), g.Count()));
        }
        static (decimal Amount, int Count) Add((decimal Amount, int Count) a, (decimal Amount, int Count) b) => (a.Amount + b.Amount, a.Count + b.Count);
        (decimal Amount, int Count) OtherFor(string institutionId, Dictionary<string, (decimal, int)> a, Dictionary<string, (decimal, int)> b, Dictionary<string, (decimal, int)> c) =>
            Add(Add(a.GetValueOrDefault(institutionId), b.GetValueOrDefault(institutionId)), c.GetValueOrDefault(institutionId));

        var lastOtherContribs = Elsewhere(lastContribsAll, lastContribs, c => c.InstitutionId, c => c.Amount);
        var lastOtherRequests = Elsewhere(lastRequestsAll, lastRequests, r => r.InstitutionId, r => r.Amount);
        var lastOtherTickets = Elsewhere(lastTicketsAll, lastTickets, t => t.InstitutionId, t => t.AmountPaid ?? 0m);
        var nextOtherContribs = Elsewhere(nextContribsAll, nextContribs, c => c.InstitutionId, c => c.Amount);
        var nextOtherRequests = Elsewhere(nextRequestsAll, nextRequests, r => r.InstitutionId, r => r.Amount);
        var nextOtherTickets = Elsewhere(nextTicketsAll, nextTickets, t => t.InstitutionId, t => t.AmountPaid ?? 0m);

        var perInstitution = institutions.Select(inst =>
        {
            var lastAmount = lastContribs.Where(c => c.InstitutionId == inst.Id).Sum(c => c.Amount)
                            + lastOrders.Where(o => o.InstitutionId == inst.Id).Sum(o => o.TotalAmount)
                            + lastRequests.Where(r => r.InstitutionId == inst.Id).Sum(r => r.Amount)
                            + lastTickets.Where(t => t.InstitutionId == inst.Id).Sum(t => t.AmountPaid ?? 0m);
            var lastCount = lastContribs.Count(c => c.InstitutionId == inst.Id) + lastOrders.Count(o => o.InstitutionId == inst.Id) + lastRequests.Count(r => r.InstitutionId == inst.Id) + lastTickets.Count(t => t.InstitutionId == inst.Id);
            var nextAmount = nextContribs.Where(c => c.InstitutionId == inst.Id).Sum(c => c.Amount)
                            + nextOrders.Where(o => o.InstitutionId == inst.Id).Sum(o => o.TotalAmount)
                            + nextRequests.Where(r => r.InstitutionId == inst.Id).Sum(r => r.Amount)
                            + nextTickets.Where(t => t.InstitutionId == inst.Id).Sum(t => t.AmountPaid ?? 0m);
            var nextCount = nextContribs.Count(c => c.InstitutionId == inst.Id) + nextOrders.Count(o => o.InstitutionId == inst.Id) + nextRequests.Count(r => r.InstitutionId == inst.Id) + nextTickets.Count(t => t.InstitutionId == inst.Id);
            var lastOther = OtherFor(inst.Id, lastOtherContribs, lastOtherRequests, lastOtherTickets);
            var nextOther = OtherFor(inst.Id, nextOtherContribs, nextOtherRequests, nextOtherTickets);

            return new InstitutionPayoutForecast(
                inst.Id, inst.Name, !string.IsNullOrWhiteSpace(inst.PaystackSubaccountCode),
                new PayoutWindowDto(windows.LastPayoutDate, lastAmount, lastCount),
                new PayoutWindowDto(windows.NextPayoutDate, nextAmount, nextCount),
                new PayoutWindowDto(windows.LastPayoutDate, lastOther.Amount, lastOther.Count),
                new PayoutWindowDto(windows.NextPayoutDate, nextOther.Amount, nextOther.Count));
        })
        .OrderByDescending(f => f.NextPayout.Amount + (f.NextOtherAccounts?.Amount ?? 0m))
        .ToList();

        var totals = new PayoutForecastTotals(
            new PayoutWindowDto(windows.LastPayoutDate, perInstitution.Sum(f => f.LastPayout.Amount), perInstitution.Sum(f => f.LastPayout.TransactionCount)),
            new PayoutWindowDto(windows.NextPayoutDate, perInstitution.Sum(f => f.NextPayout.Amount), perInstitution.Sum(f => f.NextPayout.TransactionCount)),
            new PayoutWindowDto(windows.LastPayoutDate, perInstitution.Sum(f => f.LastOtherAccounts?.Amount ?? 0m), perInstitution.Sum(f => f.LastOtherAccounts?.TransactionCount ?? 0)),
            new PayoutWindowDto(windows.NextPayoutDate, perInstitution.Sum(f => f.NextOtherAccounts?.Amount ?? 0m), perInstitution.Sum(f => f.NextOtherAccounts?.TransactionCount ?? 0)));

        return new PlatformPayoutForecastResponse(totals, perInstitution).ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetForecastAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformPayoutForecastResponse>("Failed to getforecast");
        }}

    private async Task<List<ContributionEntity>> FetchContributionsAsync(DateTime start, DateTime end) =>
        (await contributionRepo.GetAllAsync(
            c => c.Status == "Successful" && c.PaymentMethod == "Paystack" && c.ConfirmedAt != null && c.ConfirmedAt >= start && c.ConfirmedAt < end,
            ignoreQueryFilters: true)).ToList();

    private async Task<List<StoreOrderEntity>> FetchOrdersAsync(DateTime start, DateTime end) =>
        (await storeOrderRepo.GetAllAsync(
            o => o.Status == "Successful" && o.PaymentMethod == "Paystack" && o.ConfirmedAt != null && o.ConfirmedAt >= start && o.ConfirmedAt < end,
            ignoreQueryFilters: true)).ToList();

    private async Task<List<EventRsvpEntity>> FetchTicketsAsync(DateTime start, DateTime end) =>
        (await eventRsvpRepo.GetAllAsync(
            EventTicketRules.SettledBetween(start, end), ignoreQueryFilters: true)).ToList();

    private async Task<List<ServiceRequestEntity>> FetchServiceRequestsAsync(DateTime start, DateTime end) =>
        (await serviceRequestRepo.GetAllAsync(
            r => r.PaymentStatus == "Successful" && r.PaymentMethod == "Paystack" && r.ConfirmedAt != null && r.ConfirmedAt >= start && r.ConfirmedAt < end,
            ignoreQueryFilters: true)).ToList();

    /// <summary>
    /// A campaign targeting exactly one batch settles straight into that
    /// batch's own Paystack subaccount once its payout setup is approved
    /// (see Member.Api's ContributionService.ResolveSubaccountAsync, which
    /// this mirrors) — that money never reaches the institution's own
    /// account, so per-institution payout forecasts must never count it.
    /// Store orders and service requests never resolve a batch subaccount,
    /// so only contributions need this filter.
    /// </summary>
    private async Task<List<ContributionEntity>> ExcludeBatchSettledAsync(List<ContributionEntity> contributions)
    {
        if (contributions.Count == 0)
            return contributions;

        var campaignIds = contributions.Select(c => c.CampaignId).Distinct().ToList();
        var campaigns = await campaignRepo.GetQueryable(c => campaignIds.Contains(c.Id), ignoreQueryFilters: true)
            .ToDictionaryAsync(c => c.Id);

        var liveGroups = await LiveGroupIdsAsync();
        if (liveGroups.Count > 0)
            contributions = contributions
                .Where(c => !(campaigns.TryGetValue(c.CampaignId, out var gc) && gc.CommunityId != null && liveGroups.Contains(gc.CommunityId)))
                .ToList();

        var singleBatchByInstitutionYear = campaigns.Values
            .Where(c => c.YearGroups is { Count: 1 })
            .Select(c => (c.InstitutionId, Year: c.YearGroups![0]))
            .Distinct()
            .ToList();
        if (singleBatchByInstitutionYear.Count == 0)
            return contributions;

        var approvedBatches = await batchRepo.GetQueryable(
                b => b.PayoutStatus == "Approved" && !b.UseInstitutionAccount && b.PaystackSubaccountCode != null,
                ignoreQueryFilters: true)
            .Select(b => new { b.InstitutionId, b.Year })
            .ToListAsync();
        if (approvedBatches.Count == 0)
            return contributions;

        var approvedBatchKeys = approvedBatches.Select(b => (b.InstitutionId, b.Year)).ToHashSet();

        return contributions
            .Where(c => !(campaigns.TryGetValue(c.CampaignId, out var campaign)
                          && campaign.YearGroups is { Count: 1 } yg
                          && approvedBatchKeys.Contains((campaign.InstitutionId, yg[0]))))
            .ToList();
    }

    /// <summary>Groups whose own approved account receives their payments, so those never reach the institution's account.</summary>
    private async Task<HashSet<string>> LiveGroupIdsAsync() =>
        (await communityRepo.GetQueryable(
                g => g.PayoutStatus == "Approved" && !g.UseInstitutionAccount && g.PaystackSubaccountCode != null,
                ignoreQueryFilters: true)
            .Select(g => g.Id).ToListAsync()).ToHashSet();

    /// <summary>Tickets for a group's own event settle into that group's account, not the institution's.</summary>
    private async Task<List<EventRsvpEntity>> ExcludeGroupSettledTicketsAsync(List<EventRsvpEntity> tickets)
    {
        if (tickets.Count == 0) return tickets;
        var liveGroups = await LiveGroupIdsAsync();
        if (liveGroups.Count == 0) return tickets;

        var eventIds = tickets.Select(t => t.EventId).Distinct().ToList();
        var settledEvents = (await eventRepo.GetQueryable(e => eventIds.Contains(e.Id) && e.CommunityId != null, ignoreQueryFilters: true)
            .Select(e => new { e.Id, e.CommunityId }).ToListAsync())
            .Where(e => liveGroups.Contains(e.CommunityId!)).Select(e => e.Id).ToHashSet();
        return tickets.Where(t => !settledEvents.Contains(t.EventId)).ToList();
    }

    /// <summary>Service requests whose service pays into a group's or year group's own account never reach the institution's account.</summary>
    private async Task<List<ServiceRequestEntity>> ExcludeAccountSettledRequestsAsync(List<ServiceRequestEntity> requests)
    {
        if (requests.Count == 0) return requests;
        var ids = requests.Select(r => r.ServiceTypeId).Distinct().ToList();
        var services = await serviceTypeRepo.GetQueryable(s => ids.Contains(s.Id), ignoreQueryFilters: true)
            .Select(s => new { s.Id, s.InstitutionId, s.CommunityId, s.SettlementTarget }).ToListAsync();
        var audiences = services.ToDictionary(s => s.Id, s => (s.InstitutionId, Audience: SettlementAccounts.AudienceOfService(s.SettlementTarget, s.CommunityId)));

        var liveGroups = await LiveGroupIdsAsync();
        var liveYears = (await batchRepo.GetQueryable(
                b => b.PayoutStatus == "Approved" && !b.UseInstitutionAccount && b.PaystackSubaccountCode != null, ignoreQueryFilters: true)
            .Select(b => new { b.InstitutionId, b.Year }).ToListAsync()).Select(b => (b.InstitutionId, b.Year)).ToHashSet();

        var settledElsewhere = audiences.Where(kv =>
                kv.Value.Audience.CommunityId is { } g ? liveGroups.Contains(g)
                : kv.Value.Audience.Year is { } y && liveYears.Contains((kv.Value.InstitutionId, y)))
            .Select(kv => kv.Key).ToHashSet();
        return requests.Where(r => !settledElsewhere.Contains(r.ServiceTypeId)).ToList();
    }
}
