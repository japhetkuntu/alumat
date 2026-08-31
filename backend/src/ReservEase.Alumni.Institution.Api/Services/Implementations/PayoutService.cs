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
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant) : IPayoutService
{
    public async Task<IApiResponse<PayoutForecastResponse>> GetForecastAsync()
    {
        var institution = await institutionRepo.GetOneAsync(i => i.Id == currentTenant.InstitutionId);
        var payoutsConfigured = !string.IsNullOrWhiteSpace(institution?.PaystackSubaccountCode);

        var windows = PayoutWindowCalculator.GetWindows(DateTime.UtcNow);

        var (lastContribs, lastOrders) = await FetchSuccessfulPaystackAsync(windows.LastWindowStart, windows.LastWindowEnd);
        var (nextContribs, nextOrders) = await FetchSuccessfulPaystackAsync(windows.NextWindowStart, windows.NextWindowEnd);

        var lastPayout = new PayoutWindowDto(
            windows.LastPayoutDate,
            lastContribs.Sum(c => c.Amount) + lastOrders.Sum(o => o.TotalAmount),
            lastContribs.Count + lastOrders.Count);

        var nextPayout = new PayoutWindowDto(
            windows.NextPayoutDate,
            nextContribs.Sum(c => c.Amount) + nextOrders.Sum(o => o.TotalAmount),
            nextContribs.Count + nextOrders.Count);

        return new PayoutForecastResponse(payoutsConfigured, lastPayout, nextPayout).ToOkApiResponse();
    }

    private async Task<(List<Contribution> Contributions, List<StoreOrder> Orders)> FetchSuccessfulPaystackAsync(DateTime start, DateTime end)
    {
        var contributions = await contributionRepo.GetAllAsync(c =>
            c.Status == "Successful" && c.PaymentMethod == "Paystack" &&
            c.ConfirmedAt != null && c.ConfirmedAt >= start && c.ConfirmedAt < end);

        var orders = await storeOrderRepo.GetAllAsync(o =>
            o.Status == "Successful" && o.PaymentMethod == "Paystack" &&
            o.ConfirmedAt != null && o.ConfirmedAt >= start && o.ConfirmedAt < end);

        return (contributions.ToList(), orders.ToList());
    }
}
