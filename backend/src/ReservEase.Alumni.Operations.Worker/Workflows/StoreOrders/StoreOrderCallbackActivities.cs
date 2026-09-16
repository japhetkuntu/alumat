using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
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
    IAlumniPgRepository<StoreProduct> productRepo,
    IAlumniPgRepository<StoreProductVariant> variantRepo,
    IPaystackService paystackService,
    ILogger<StoreOrderCallbackActivities> logger)
{
    [Activity("StoreOrderCallback.LoadOrder")]
    public virtual Task<StoreOrder?> LoadOrderAsync(string reference) =>
        Wrap(() => orderRepo.GetOneAsync(o => o.TransactionRef == reference, ignoreQueryFilters: true), "load store order", reference);

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
        Wrap(() => productRepo.GetOneAsync(p => p.Id == productId, ignoreQueryFilters: true), "load store product", productId);

    [Activity("StoreOrderCallback.LoadVariant")]
    public virtual Task<StoreProductVariant?> LoadVariantAsync(string variantId) =>
        Wrap(() => variantRepo.GetOneAsync(v => v.Id == variantId, ignoreQueryFilters: true), "load store product variant", variantId);

    [Activity("StoreOrderCallback.SaveVariantStock")]
    public virtual Task SaveVariantStockAsync(StoreProductVariant variant) =>
        Wrap(() => variantRepo.ExecuteUpdateAsync(
            v => v.Id == variant.Id,
            s => s.SetProperty(v => v.QuantityAvailable, variant.QuantityAvailable),
            ignoreQueryFilters: true), "save variant stock", variant.Id);

    [Activity("StoreOrderCallback.LoadVariantsForProduct")]
    public virtual Task<List<StoreProductVariant>> LoadVariantsForProductAsync(string productId) =>
        Wrap(() => variantRepo.GetQueryable(v => v.ProductId == productId, ignoreQueryFilters: true).ToListAsync(), "load variants for product", productId);

    [Activity("StoreOrderCallback.SaveProductStock")]
    public virtual Task SaveProductStockAsync(string productId, int quantityAvailable) =>
        Wrap(() => productRepo.ExecuteUpdateAsync(
            p => p.Id == productId,
            s => s.SetProperty(p => p.QuantityAvailable, quantityAvailable),
            ignoreQueryFilters: true), "save product stock", productId);

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
