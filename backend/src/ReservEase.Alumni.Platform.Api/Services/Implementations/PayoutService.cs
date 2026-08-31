using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;
using StoreOrderEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.StoreOrder;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Cross-tenant mirror of Institution.Api's own PayoutService — same
/// <see cref="PayoutWindowCalculator"/> windows, same successful+Paystack
/// filter, same sum, just grouped by institution instead of scoped to one.
/// Deliberately duplicated rather than shared as a service (Platform.Api has
/// no tenant context to inject), but every institution's figures computed
/// here are guaranteed to equal what that institution's own SuperAdmins see,
/// since both start from PayoutWindowCalculator and the same raw formula.
/// </summary>
public class PayoutService(AlumniDbContext db) : IPayoutService
{
    public async Task<IApiResponse<PlatformPayoutForecastResponse>> GetForecastAsync()
    {
        var windows = PayoutWindowCalculator.GetWindows(DateTime.UtcNow);

        var institutions = await db.Institutions.IgnoreQueryFilters()
            .Select(i => new { i.Id, i.Name, i.PaystackSubaccountCode })
            .ToListAsync();

        var lastContribs = await FetchContributionsAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var lastOrders = await FetchOrdersAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var nextContribs = await FetchContributionsAsync(windows.NextWindowStart, windows.NextWindowEnd);
        var nextOrders = await FetchOrdersAsync(windows.NextWindowStart, windows.NextWindowEnd);

        var perInstitution = institutions.Select(inst =>
        {
            var lastAmount = lastContribs.Where(c => c.InstitutionId == inst.Id).Sum(c => c.Amount)
                            + lastOrders.Where(o => o.InstitutionId == inst.Id).Sum(o => o.TotalAmount);
            var lastCount = lastContribs.Count(c => c.InstitutionId == inst.Id) + lastOrders.Count(o => o.InstitutionId == inst.Id);
            var nextAmount = nextContribs.Where(c => c.InstitutionId == inst.Id).Sum(c => c.Amount)
                            + nextOrders.Where(o => o.InstitutionId == inst.Id).Sum(o => o.TotalAmount);
            var nextCount = nextContribs.Count(c => c.InstitutionId == inst.Id) + nextOrders.Count(o => o.InstitutionId == inst.Id);

            return new InstitutionPayoutForecast(
                inst.Id, inst.Name, !string.IsNullOrWhiteSpace(inst.PaystackSubaccountCode),
                new PayoutWindowDto(windows.LastPayoutDate, lastAmount, lastCount),
                new PayoutWindowDto(windows.NextPayoutDate, nextAmount, nextCount));
        })
        .OrderByDescending(f => f.NextPayout.Amount)
        .ToList();

        var totals = new PayoutForecastTotals(
            new PayoutWindowDto(windows.LastPayoutDate, perInstitution.Sum(f => f.LastPayout.Amount), perInstitution.Sum(f => f.LastPayout.TransactionCount)),
            new PayoutWindowDto(windows.NextPayoutDate, perInstitution.Sum(f => f.NextPayout.Amount), perInstitution.Sum(f => f.NextPayout.TransactionCount)));

        return new PlatformPayoutForecastResponse(totals, perInstitution).ToOkApiResponse();
    }

    private Task<List<ContributionEntity>> FetchContributionsAsync(DateTime start, DateTime end) =>
        db.Set<ContributionEntity>().IgnoreQueryFilters()
            .Where(c => c.Status == "Successful" && c.PaymentMethod == "Paystack" && c.ConfirmedAt != null && c.ConfirmedAt >= start && c.ConfirmedAt < end)
            .ToListAsync();

    private Task<List<StoreOrderEntity>> FetchOrdersAsync(DateTime start, DateTime end) =>
        db.Set<StoreOrderEntity>().IgnoreQueryFilters()
            .Where(o => o.Status == "Successful" && o.PaymentMethod == "Paystack" && o.ConfirmedAt != null && o.ConfirmedAt >= start && o.ConfirmedAt < end)
            .ToListAsync();
}
