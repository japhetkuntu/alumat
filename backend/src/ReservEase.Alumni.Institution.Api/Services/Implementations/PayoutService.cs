using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// Estimates what Paystack will settle to this institution's own bank
/// account — see <see cref="PayoutWindowCalculator"/> for the settlement-window
/// model and why this is computed from our own confirmed transactions rather
/// than a Paystack Settlement API call.
/// </summary>
public class PayoutService(
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<StoreOrder> storeOrderRepo,
    IAlumniPgRepository<ServiceRequest> serviceRequestRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant) : IPayoutService
{
    public async Task<IApiResponse<PayoutForecastResponse>> GetForecastAsync()
    {
        var institution = await institutionRepo.GetOneAsync(i => i.Id == currentTenant.InstitutionId);
        var payoutsConfigured = !string.IsNullOrWhiteSpace(institution?.PaystackSubaccountCode);

        var windows = PayoutWindowCalculator.GetWindows(DateTime.UtcNow);

        var (lastContribs, lastOrders, lastRequests) = await FetchSuccessfulPaystackAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var (nextContribs, nextOrders, nextRequests) = await FetchSuccessfulPaystackAsync(windows.NextWindowStart, windows.NextWindowEnd);

        var lastPayout = new PayoutWindowDto(
            windows.LastPayoutDate,
            lastContribs.Sum(c => c.Amount) + lastOrders.Sum(o => o.TotalAmount) + lastRequests.Sum(r => r.Amount),
            lastContribs.Count + lastOrders.Count + lastRequests.Count);

        var nextPayout = new PayoutWindowDto(
            windows.NextPayoutDate,
            nextContribs.Sum(c => c.Amount) + nextOrders.Sum(o => o.TotalAmount) + nextRequests.Sum(r => r.Amount),
            nextContribs.Count + nextOrders.Count + nextRequests.Count);

        return new PayoutForecastResponse(payoutsConfigured, lastPayout, nextPayout).ToOkApiResponse();
    }

    private async Task<(List<Contribution> Contributions, List<StoreOrder> Orders, List<ServiceRequest> Requests)> FetchSuccessfulPaystackAsync(DateTime start, DateTime end)
    {
        var contributions = await contributionRepo.GetAllAsync(c =>
            c.Status == "Successful" && c.PaymentMethod == "Paystack" &&
            c.ConfirmedAt != null && c.ConfirmedAt >= start && c.ConfirmedAt < end);

        var orders = await storeOrderRepo.GetAllAsync(o =>
            o.Status == "Successful" && o.PaymentMethod == "Paystack" &&
            o.ConfirmedAt != null && o.ConfirmedAt >= start && o.ConfirmedAt < end);

        var requests = await serviceRequestRepo.GetAllAsync(r =>
            r.PaymentStatus == "Successful" && r.PaymentMethod == "Paystack" &&
            r.ConfirmedAt != null && r.ConfirmedAt >= start && r.ConfirmedAt < end);

        // Store orders and service requests always settle to the institution's own
        // account (see StoreOrderService/ServiceRequestService — neither resolves a
        // batch subaccount), so only contributions need filtering here. A campaign
        // targeting exactly one batch settles straight into THAT batch's own Paystack
        // subaccount once its payout setup is approved (see ContributionService.
        // ResolveSubaccountAsync, which this mirrors) — money that never reaches this
        // institution's own account, so it must never be counted as if it will.
        var institutionSettledContributions = await ExcludeBatchSettledAsync(contributions.ToList());

        return (institutionSettledContributions, orders.ToList(), requests.ToList());
    }

    private async Task<List<Contribution>> ExcludeBatchSettledAsync(List<Contribution> contributions)
    {
        if (contributions.Count == 0)
            return contributions;

        var campaignIds = contributions.Select(c => c.CampaignId).Distinct().ToList();
        var campaigns = (await campaignRepo.GetAllAsync(c => campaignIds.Contains(c.Id)))
            .ToDictionary(c => c.Id);

        var singleBatchYears = campaigns.Values
            .Where(c => c.YearGroups is { Count: 1 })
            .Select(c => c.YearGroups![0])
            .Distinct()
            .ToList();
        if (singleBatchYears.Count == 0)
            return contributions;

        var approvedBatchYears = (await batchRepo.GetAllAsync(b =>
                singleBatchYears.Contains(b.Year) && b.PayoutStatus == "Approved" && !b.UseInstitutionAccount && b.PaystackSubaccountCode != null))
            .Select(b => b.Year)
            .ToHashSet();
        if (approvedBatchYears.Count == 0)
            return contributions;

        return contributions
            .Where(c => !(campaigns.TryGetValue(c.CampaignId, out var campaign)
                          && campaign.YearGroups is { Count: 1 } yg
                          && approvedBatchYears.Contains(yg[0])))
            .ToList();
    }
}
