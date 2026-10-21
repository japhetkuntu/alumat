using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Microsoft.EntityFrameworkCore;
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
    IAlumniPgRepository<EventRsvp> eventRsvpRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant,
    ILogger<PayoutService> logger) : IPayoutService
{
    public async Task<IApiResponse<PayoutForecastResponse>> GetForecastAsync()
    {
        try
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == currentTenant.InstitutionId);
            var payoutsConfigured = !string.IsNullOrWhiteSpace(institution?.PaystackSubaccountCode);

            var windows = PayoutWindowCalculator.GetWindows(DateTime.UtcNow);
            var lastStart = windows.LastWindowStart; var lastEnd = windows.LastWindowEnd;
            var nextStart = windows.NextWindowStart; var nextEnd = windows.NextWindowEnd;

            // Totals are worked out in the database and only the sums come back, one small query per kind of payment for both
            // windows together, instead of loading every payment row (with its snapshots) just to add them up. A payment is
            // told apart by when it was confirmed: before nextStart is the last window, from it on is the next one.
            var contributionTotals = await contributionRepo.GetQueryable(c =>
                    c.Status == "Successful" && c.PaymentMethod == "Paystack" && c.ConfirmedAt != null
                    && ((c.ConfirmedAt >= lastStart && c.ConfirmedAt < lastEnd) || (c.ConfirmedAt >= nextStart && c.ConfirmedAt < nextEnd)))
                .GroupBy(c => new { c.CampaignId, IsNext = c.ConfirmedAt >= nextStart })
                .Select(g => new { g.Key.CampaignId, g.Key.IsNext, Amount = g.Sum(c => c.Amount), Count = g.Count() })
                .ToListAsync();
            var batchSettled = await BatchSettledCampaignIdsAsync(contributionTotals.Select(c => c.CampaignId).Distinct().ToList());

            var orderTotals = await storeOrderRepo.GetQueryable(o =>
                    o.Status == "Successful" && o.PaymentMethod == "Paystack" && o.ConfirmedAt != null
                    && ((o.ConfirmedAt >= lastStart && o.ConfirmedAt < lastEnd) || (o.ConfirmedAt >= nextStart && o.ConfirmedAt < nextEnd)))
                .GroupBy(o => o.ConfirmedAt >= nextStart)
                .Select(g => new { IsNext = g.Key, Amount = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync();

            var requestTotals = await serviceRequestRepo.GetQueryable(r =>
                    r.PaymentStatus == "Successful" && r.PaymentMethod == "Paystack" && r.ConfirmedAt != null
                    && ((r.ConfirmedAt >= lastStart && r.ConfirmedAt < lastEnd) || (r.ConfirmedAt >= nextStart && r.ConfirmedAt < nextEnd)))
                .GroupBy(r => r.ConfirmedAt >= nextStart)
                .Select(g => new { IsNext = g.Key, Amount = g.Sum(r => r.Amount), Count = g.Count() })
                .ToListAsync();

            // Event tickets settle to the institution's own account like store orders and service requests do.
            var ticketTotals = await eventRsvpRepo.GetQueryable(EventTicketRules.SettledInEither(lastStart, lastEnd, nextStart, nextEnd))
                .GroupBy(t => t.PaidAt >= nextStart)
                .Select(g => new { IsNext = g.Key, Amount = g.Sum(t => t.AmountPaid ?? 0m), Count = g.Count() })
                .ToListAsync();

            PayoutWindowDto Window(DateOnly date, bool next)
            {
                var amount = contributionTotals.Where(c => c.IsNext == next && !batchSettled.Contains(c.CampaignId)).Sum(c => c.Amount)
                    + orderTotals.Where(o => o.IsNext == next).Sum(o => o.Amount)
                    + requestTotals.Where(r => r.IsNext == next).Sum(r => r.Amount)
                    + ticketTotals.Where(t => t.IsNext == next).Sum(t => t.Amount);
                var count = contributionTotals.Where(c => c.IsNext == next && !batchSettled.Contains(c.CampaignId)).Sum(c => c.Count)
                    + orderTotals.Where(o => o.IsNext == next).Sum(o => o.Count)
                    + requestTotals.Where(r => r.IsNext == next).Sum(r => r.Count)
                    + ticketTotals.Where(t => t.IsNext == next).Sum(t => t.Count);
                return new PayoutWindowDto(date, amount, count);
            }

            return new PayoutForecastResponse(payoutsConfigured, Window(windows.LastPayoutDate, false), Window(windows.NextPayoutDate, true)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetForecastAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PayoutForecastResponse>("Failed to getforecast");
        }
    }

    /// <summary>
    /// A campaign targeting exactly one batch settles straight into THAT batch's own Paystack subaccount once its payout setup is
    /// approved (see ContributionService.ResolveSubaccountAsync, which this mirrors) — money that never reaches this institution's
    /// own account, so it must never be counted as if it will. Store orders and service requests never resolve a batch subaccount,
    /// so only contributions need this. Returns the ids of the campaigns whose money goes elsewhere.
    /// </summary>
    private async Task<HashSet<string>> BatchSettledCampaignIdsAsync(List<string> campaignIds)
    {
        if (campaignIds.Count == 0) return [];

        var campaigns = await campaignRepo.GetQueryable(c => campaignIds.Contains(c.Id)).Select(c => new { c.Id, c.YearGroups }).ToListAsync();
        var singleYear = campaigns.Where(c => c.YearGroups is { Count: 1 }).ToDictionary(c => c.Id, c => c.YearGroups![0]);
        if (singleYear.Count == 0) return [];

        var years = singleYear.Values.Distinct().ToList();
        var approvedYears = (await batchRepo.GetQueryable(b =>
                years.Contains(b.Year) && b.PayoutStatus == "Approved" && !b.UseInstitutionAccount && b.PaystackSubaccountCode != null)
            .Select(b => b.Year).ToListAsync()).ToHashSet();

        return singleYear.Where(kv => approvedYears.Contains(kv.Value)).Select(kv => kv.Key).ToHashSet();
    }
}
