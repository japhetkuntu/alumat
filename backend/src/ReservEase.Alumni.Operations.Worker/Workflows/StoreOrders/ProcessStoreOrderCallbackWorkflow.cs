using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.StoreOrders;

[Workflow("ProcessStoreOrderCallback")]
public class ProcessStoreOrderCallbackWorkflow : IProcessStoreOrderCallbackWorkflow
{
    [WorkflowRun]
    public async Task<PaymentCallbackResult> RunAsync(ProcessPaymentCallbackRequest request)
    {
        var reference = request.Reference;
        Workflow.Logger.LogInformation(
            "[StoreOrderCallback] Processing {Provider} callback for reference {Reference}",
            request.Provider, reference);

        var order = await Workflow.ExecuteActivityAsync(
            (StoreOrderCallbackActivities a) => a.LoadOrderAsync(reference),
            PaymentActivityOptions.DatabaseRead);

        if (order is null)
        {
            // We only ever call Paystack's InitializePaymentAsync after our own
            // StoreOrder row is durably committed, so a callback for a reference
            // with no matching row can't be our own data loss — it's a wrong or
            // bogus reference. Reject rather than accepting it silently.
            Workflow.Logger.LogError("No StoreOrder found for reference {Reference}. Rejecting callback as an unknown reference.", reference);
            return PaymentCallbackResult.BadRequest("Unknown payment reference.");
        }

        if (!string.IsNullOrEmpty(request.RawBody))
            order.CallbackPayload = request.RawBody;

        if (order.Status == "Successful")
        {
            if (!string.IsNullOrEmpty(request.RawBody))
                await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.SaveOrderAsync(order), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.Ok("Payment already verified and recorded");
        }

        var verify = await Workflow.ExecuteActivityAsync(
            (StoreOrderCallbackActivities a) => a.VerifyPaystackPaymentAsync(reference),
            PaymentActivityOptions.ExternalGateway);

        if (!verify.Status)
        {
            order.Status = "Failed";
            order.FailureMessage = verify.Message;
            await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.SaveOrderAsync(order), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.BadRequest(verify.Message ?? "Payment verification failed");
        }

        order.GrossChargeAmount = verify.GrossAmount;
        if (verify.GatewayFee.HasValue)
            order.GatewayFeeAmount = verify.GatewayFee.Value;
        order.GatewayResponse = verify.GatewayResponse;

        if (!string.IsNullOrEmpty(request.RawBody))
        {
            try
            {
                var payload = JObject.Parse(request.RawBody);
                order.Channel ??= payload.SelectToken("data.authorization.channel")?.ToString();
            }
            catch
            {
                // best effort; ignore if parsing fails
            }
        }

        if (verify.PaystackStatus == "success")
        {
            order.Status = "Successful";
            order.ConfirmedAt = Workflow.UtcNow;

            // Best-effort inventory decrement — clamped at zero rather than blocking
            // an already-paid order; simultaneous last-unit checkouts are a known,
            // accepted edge case for v1.
            var touchedProductIds = new HashSet<string>();
            foreach (var item in order.Items)
            {
                var product = await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.LoadProductAsync(item.ProductId), PaymentActivityOptions.DatabaseRead);
                if (product is null) continue;

                if (!string.IsNullOrEmpty(item.VariantId))
                {
                    var variant = await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.LoadVariantAsync(item.VariantId), PaymentActivityOptions.DatabaseRead);
                    if (variant is not null)
                    {
                        var before = variant.QuantityAvailable;
                        variant.QuantityAvailable = Math.Max(0, variant.QuantityAvailable - item.Quantity);
                        if (before < item.Quantity)
                            Workflow.Logger.LogWarning("Store product variant {VariantId} oversold on order {OrderId}: requested {Requested}, had {Available}", variant.Id, order.Id, item.Quantity, before);
                        await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.SaveVariantStockAsync(variant), PaymentActivityOptions.DatabaseWrite);
                        touchedProductIds.Add(product.Id);
                    }
                }
                else
                {
                    var before = product.QuantityAvailable;
                    var newQty = Math.Max(0, product.QuantityAvailable - item.Quantity);
                    if (before < item.Quantity)
                        Workflow.Logger.LogWarning("Store product {ProductId} oversold on order {OrderId}: requested {Requested}, had {Available}", product.Id, order.Id, item.Quantity, before);
                    await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.SaveProductStockAsync(product.Id, newQty), PaymentActivityOptions.DatabaseWrite);
                }
            }

            // Roll the parent product's QuantityAvailable back up from its variants so
            // grid-level stock displays stay correct after a variant-level decrement above.
            foreach (var productId in touchedProductIds)
            {
                var variants = await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.LoadVariantsForProductAsync(productId), PaymentActivityOptions.DatabaseRead);
                var total = variants.Sum(v => v.QuantityAvailable);
                await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.SaveProductStockAsync(productId, total), PaymentActivityOptions.DatabaseWrite);
            }
        }
        else
        {
            order.Status = "Failed";
            order.FailureMessage = $"Payment {verify.PaystackStatus}.";
        }

        await Workflow.ExecuteActivityAsync((StoreOrderCallbackActivities a) => a.SaveOrderAsync(order), PaymentActivityOptions.DatabaseWrite);
        return order.Status == "Successful"
            ? PaymentCallbackResult.Ok("Payment verified and store order confirmed")
            : PaymentCallbackResult.Ok("Payment status updated");
    }
}
