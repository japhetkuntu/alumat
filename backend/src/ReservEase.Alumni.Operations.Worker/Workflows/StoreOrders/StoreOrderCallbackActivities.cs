using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker.Workflows.StoreOrders;

/// <summary>
/// One activity, one I/O call — the workflow (ProcessStoreOrderCallbackWorkflow)
/// owns the sequencing and every branch/decision. Registered via
/// AddScopedActivities, so each call gets its own DI scope automatically.
/// </summary>
public class StoreOrderCallbackActivities(
    IAlumniPgRepository<StoreOrder> orderRepo,
    AlumniDbContext db,
    IPaystackService paystackService,
    ILogger<StoreOrderCallbackActivities> logger)
{
    [Activity("StoreOrderCallback.LoadOrder")]
    public virtual Task<StoreOrder?> LoadOrderAsync(string reference) =>
        Wrap(() => db.Set<StoreOrder>().IgnoreQueryFilters().FirstOrDefaultAsync(o => o.TransactionRef == reference), "load store order", reference);

    [Activity("StoreOrderCallback.SaveOrder")]
    public virtual Task SaveOrderAsync(StoreOrder order) =>
        Wrap(() => orderRepo.UpdateAsync(order), "save store order", order.Id);

    [Activity("StoreOrderCallback.VerifyPaystackPayment")]
    public virtual Task<StoreOrderPaystackVerifyResult> VerifyPaystackPaymentAsync(string reference) =>
        Wrap(async () =>
        {
            var response = await paystackService.VerifyPaymentAsync(reference);
            var amount = (response.Data?.Amount ?? 0) / 100m;
            var fee = response.Data?.Fees.HasValue == true ? response.Data!.Fees!.Value / 100m : (decimal?)null;
            return new StoreOrderPaystackVerifyResult
            {
                Status = response.Status,
                Message = response.Message,
                PaystackStatus = response.Data?.Status?.ToLowerInvariant() ?? "unknown",
                GrossAmount = amount,
                GatewayFee = fee,
                GatewayResponse = response.Data?.GatewayResponse,
            };
        }, "verify Paystack payment", reference);

    [Activity("StoreOrderCallback.LoadProduct")]
    public virtual Task<StoreProduct?> LoadProductAsync(string productId) =>
        Wrap(() => db.Set<StoreProduct>().IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == productId), "load store product", productId);

    [Activity("StoreOrderCallback.LoadVariant")]
    public virtual Task<StoreProductVariant?> LoadVariantAsync(string variantId) =>
        Wrap(() => db.Set<StoreProductVariant>().IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == variantId), "load store product variant", variantId);

    [Activity("StoreOrderCallback.SaveVariantStock")]
    public virtual Task SaveVariantStockAsync(StoreProductVariant variant) =>
        Wrap(() => db.Set<StoreProductVariant>().Where(v => v.Id == variant.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.QuantityAvailable, variant.QuantityAvailable)), "save variant stock", variant.Id);

    [Activity("StoreOrderCallback.LoadVariantsForProduct")]
    public virtual Task<List<StoreProductVariant>> LoadVariantsForProductAsync(string productId) =>
        Wrap(() => db.Set<StoreProductVariant>().IgnoreQueryFilters().Where(v => v.ProductId == productId).ToListAsync(), "load variants for product", productId);

    [Activity("StoreOrderCallback.SaveProductStock")]
    public virtual Task SaveProductStockAsync(string productId, int quantityAvailable) =>
        Wrap(() => db.Set<StoreProduct>().Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.QuantityAvailable, quantityAvailable)), "save product stock", productId);

    private static async Task<T> Wrap<T>(Func<Task<T>> action, string what, string context)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }

    private static async Task Wrap(Func<Task> action, string what, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }
}
