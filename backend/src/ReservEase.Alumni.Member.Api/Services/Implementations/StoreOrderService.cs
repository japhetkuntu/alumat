using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Store checkout — deliberately its own self-contained flow rather than
/// reused Contribution/PaymentTransaction plumbing (that pair exists for
/// Campaign-specific concerns like guest payments and repeated per-campaign
/// giving that Store doesn't have). Same Zero-Deduction fee mechanics as
/// ContributionService.BuildZeroDeductionCharge, and the same Paystack
/// webhook route dispatches here via IStoreOrderService.OwnsReferenceAsync
/// (see PaystackCallbackActor) — but StoreOrder itself is both the pending
/// AND the confirmed record, so there's no separate transaction table.
/// </summary>
public class StoreOrderService(
    IAlumniPgRepository<StoreOrder> orderRepo,
    IAlumniPgRepository<StoreProduct> productRepo,
    IAlumniPgRepository<StoreProductVariant> variantRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ICurrentTenantService currentTenant,
    AlumniDbContext db,
    IPaystackService paystackService,
    PaystackConfig paystackConfig,
    IConfiguration configuration,
    ILogger<StoreOrderService> logger) : IStoreOrderService
{
    private readonly string _paystackCallbackUrl = BuildCallbackUrl(configuration);

    private static string BuildCallbackUrl(IConfiguration configuration)
    {
        var callbackUrl = configuration["PaystackConfig:CallbackUrl"] ?? string.Empty;
        if (!string.IsNullOrEmpty(callbackUrl) && !callbackUrl.EndsWith("/callback", StringComparison.OrdinalIgnoreCase))
            callbackUrl = callbackUrl.TrimEnd('/') + "/callback";
        return callbackUrl;
    }

    private async Task<Institution?> GetCurrentInstitutionAsync() =>
        string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);

    /// <summary>Mirrors ContributionService.BuildZeroDeductionCharge — the institution nets 100% of totalAmount, our fee is collected from the payer's grossed-up charge.</summary>
    private (long amountSubunit, long? transactionCharge, string? bearer, decimal platformFee, decimal gatewayFee, decimal transactionChargeAmount, decimal grossCharge)
        BuildZeroDeductionCharge(decimal totalAmount, Institution? institution)
    {
        var totalSubunit = (long)Math.Round(totalAmount * 100m, MidpointRounding.AwayFromZero);

        if (institution is null || string.IsNullOrEmpty(institution.PaystackSubaccountCode))
            return (totalSubunit, null, null, 0m, 0m, 0m, totalAmount);

        var charge = PaystackFeeCalculator.CalculateZeroDeductionCharge(
            totalSubunit,
            institution.PlatformFeePercentage,
            paystackConfig.GatewayFeePercentage,
            paystackConfig.GatewayFixedFeeSubunit,
            paystackConfig.GatewayFeeCapSubunit,
            paystackConfig.GatewayFeeSafetyBufferSubunit);

        return (
            charge.ChargeAmountSubunit,
            charge.TransactionChargeSubunit,
            "account",
            charge.PlatformFeeSubunit / 100m,
            charge.GatewayFeeSubunit / 100m,
            charge.TransactionChargeSubunit / 100m,
            charge.ChargeAmountSubunit / 100m);
    }

    public async Task<IApiResponse<PgPagedResult<StoreProductDto>>> GetProductsAsync(StoreProductFilter filter)
    {
        try
        {
            var result = await productRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                p => p.Status == "Active"
                  && (string.IsNullOrEmpty(filter.Search) || p.Name.Contains(filter.Search)));

            var productIds = result.Results.Select(p => p.Id).ToList();
            var variantsByProduct = (await variantRepo.GetAllAsync(v => productIds.Contains(v.ProductId)))
                .GroupBy(v => v.ProductId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var dtoResult = new PgPagedResult<StoreProductDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(p => p.ToDto(variantsByProduct.GetValueOrDefault(p.Id))).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving store products — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<StoreProductDto>>("Failed to retrieve products");
        }
    }

    public async Task<IApiResponse<StoreProductDto>> GetProductByIdAsync(string productId)
    {
        try
        {
            var product = await productRepo.GetByIdAsync(productId);
            if (product is null || product.Status != "Active")
                return ApiResponseExtensions.ToNotFoundApiResponse<StoreProductDto>("Product not found");

            var variants = (await variantRepo.GetAllAsync(v => v.ProductId == productId)).ToList();
            return product.ToDto(variants).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving store product {ProductId}", productId);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreProductDto>("Failed to retrieve product");
        }
    }

    public async Task<IApiResponse<StoreCheckoutResponse>> InitiateCheckoutAsync(CheckoutRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("InitiateCheckout request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            if (request.Items is not { Count: > 0 })
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>("Your cart is empty.");

            var items = new List<StoreOrderItem>();
            decimal total = 0m;
            foreach (var line in request.Items)
            {
                if (line.Quantity <= 0)
                    return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>("Quantity must be greater than zero.");

                var product = await productRepo.GetByIdAsync(line.ProductId);
                if (product is null || product.Status != "Active")
                    return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>($"A product in your cart is no longer available.");

                if (product.VariantOptionTypes is { Count: > 0 })
                {
                    // Variant product — the line must name a specific variant, and stock/price come from that variant, not the product roll-up.
                    if (string.IsNullOrWhiteSpace(line.VariantId))
                        return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>($"Please choose an option for \"{product.Name}\".");

                    var variant = await variantRepo.GetByIdAsync(line.VariantId);
                    if (variant is null || variant.ProductId != product.Id)
                        return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>($"The selected option for \"{product.Name}\" is no longer available.");
                    if (variant.QuantityAvailable < line.Quantity)
                        return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>($"Only {variant.QuantityAvailable} of \"{product.Name}\" left in stock.");

                    var variantPrice = variant.PriceOverride ?? product.Price;
                    items.Add(new StoreOrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductImageUrl = variant.ImageUrl ?? product.ImageUrls?.FirstOrDefault(),
                        UnitPrice = variantPrice,
                        Quantity = line.Quantity,
                        DeliveryInfo = product.DeliveryInfo,
                        VariantId = variant.Id,
                        VariantOptions = variant.Options,
                        Sku = variant.Sku,
                    });
                    total += variantPrice * line.Quantity;
                }
                else
                {
                    if (product.QuantityAvailable < line.Quantity)
                        return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>($"Only {product.QuantityAvailable} of \"{product.Name}\" left in stock.");

                    items.Add(new StoreOrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductImageUrl = product.ImageUrls?.FirstOrDefault(),
                        UnitPrice = product.Price,
                        Quantity = line.Quantity,
                        DeliveryInfo = product.DeliveryInfo,
                    });
                    total += product.Price * line.Quantity;
                }
            }

            var currentInstitution = await GetCurrentInstitutionAsync();
            var charge = BuildZeroDeductionCharge(total, currentInstitution);

            logger.LogInformation(
                "Zero-Deduction charge for store checkout by member {MemberId}, institution {InstitutionId}: total={Total}, platformFee={PlatformFee}, gatewayFee={GatewayFee}, chargeAmount={ChargeAmount}, transactionCharge={TransactionCharge}",
                member.Id, currentInstitution?.Id, total, charge.platformFee, charge.gatewayFee, charge.grossCharge, charge.transactionCharge);

            var response = await paystackService.InitializePaymentAsync(new InitializePaymentRequest
            {
                Email = member.Email,
                Amount = charge.amountSubunit,
                CallbackUrl = !string.IsNullOrWhiteSpace(request.CallbackUrl) ? request.CallbackUrl : _paystackCallbackUrl,
                Metadata = new Dictionary<string, string> { { "memberId", member.Id }, { "storeOrder", "true" } },
                Subaccount = currentInstitution?.PaystackSubaccountCode,
                TransactionCharge = charge.transactionCharge,
                Bearer = charge.bearer,
            });

            if (!response.Status)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreCheckoutResponse>(response.Message);

            var reference = response.Data?.Reference ?? string.Empty;

            var order = new StoreOrder
            {
                OrderNumber = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                MemberId = member.Id,
                Member = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                },
                Items = items,
                TotalAmount = total,
                Status = "Pending",
                PaymentMethod = "Paystack",
                TransactionRef = reference,
                PlatformFeeAmount = charge.platformFee,
                GatewayFeeAmount = charge.gatewayFee,
                TransactionChargeAmount = charge.transactionChargeAmount,
                GrossChargeAmount = charge.grossCharge,
                CreatedBy = member.Id,
            };

            await orderRepo.AddAsync(order);
            logger.LogInformation("Store checkout initiated for member {MemberId}, order {OrderId}, reference {Reference}", member.Id, order.Id, reference);

            return new StoreCheckoutResponse { AuthorizationUrl = response.Data?.AuthorizationUrl, Reference = reference }
                .ToOkApiResponse("Checkout initiated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error initiating store checkout for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreCheckoutResponse>("Failed to initiate checkout");
        }
    }

    public async Task<bool> OwnsReferenceAsync(string reference)
    {
        return await db.Set<StoreOrder>().IgnoreQueryFilters().AnyAsync(o => o.TransactionRef == reference);
    }

    public async Task<IApiResponse<object>> ProcessPaystackCallbackAsync(string reference, string rawBody)
    {
        try
        {
            await ProcessReferenceAsync(reference, rawBody);
            return ApiResponseExtensions.ToOkApiResponse<object>("Callback processed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing store Paystack callback for reference {Reference}", reference);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to process callback");
        }
    }

    private async Task ProcessReferenceAsync(string reference, string? rawBody = null)
    {
        var order = await db.Set<StoreOrder>().IgnoreQueryFilters().FirstOrDefaultAsync(o => o.TransactionRef == reference);
        if (order is null)
        {
            logger.LogWarning("Store order not found for Paystack reference {Reference}", reference);
            return;
        }

        if (!string.IsNullOrEmpty(rawBody))
            order.CallbackPayload = rawBody;

        if (order.Status == "Successful")
        {
            if (!string.IsNullOrEmpty(rawBody))
                await db.SaveChangesAsync();
            return; // Already processed — webhook + poll fallback can both fire.
        }

        var verifyResponse = await paystackService.VerifyPaymentAsync(reference);
        if (!verifyResponse.Status)
        {
            order.Status = "Failed";
            order.FailureMessage = verifyResponse.Message;
            await db.SaveChangesAsync();
            return;
        }

        var paystackStatus = verifyResponse.Data?.Status?.ToLowerInvariant() ?? "unknown";
        var verifiedGrossAmount = (verifyResponse.Data?.Amount ?? 0) / 100m;
        order.GrossChargeAmount = verifiedGrossAmount;
        if (verifyResponse.Data?.Fees.HasValue == true)
            order.GatewayFeeAmount = verifyResponse.Data!.Fees!.Value / 100m;

        order.GatewayResponse = verifyResponse.Data?.GatewayResponse;

        if (!string.IsNullOrEmpty(rawBody))
        {
            try
            {
                var payload = JObject.Parse(rawBody);
                order.Channel ??= payload.SelectToken("data.authorization.channel")?.ToString();
            }
            catch
            {
                // best effort; ignore if parsing fails
            }
        }

        if (paystackStatus == "success")
        {
            order.Status = "Successful";
            order.ConfirmedAt = DateTime.UtcNow;

            // Best-effort inventory decrement — clamped at zero rather than
            // blocking an already-paid order; simultaneous last-unit
            // checkouts are a known, accepted edge case for v1.
            var touchedProductIds = new HashSet<string>();
            foreach (var item in order.Items)
            {
                var product = await db.Set<StoreProduct>().IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product is null) continue;

                if (!string.IsNullOrEmpty(item.VariantId))
                {
                    var variant = await db.Set<StoreProductVariant>().IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == item.VariantId);
                    if (variant is not null)
                    {
                        variant.QuantityAvailable = Math.Max(0, variant.QuantityAvailable - item.Quantity);
                        if (variant.QuantityAvailable < item.Quantity)
                            logger.LogWarning("Store product variant {VariantId} oversold on order {OrderId}: requested {Requested}, had {Available}", variant.Id, order.Id, item.Quantity, variant.QuantityAvailable + item.Quantity);
                        touchedProductIds.Add(product.Id);
                    }
                }
                else
                {
                    product.QuantityAvailable = Math.Max(0, product.QuantityAvailable - item.Quantity);
                    if (product.QuantityAvailable < item.Quantity)
                        logger.LogWarning("Store product {ProductId} oversold on order {OrderId}: requested {Requested}, had {Available}", product.Id, order.Id, item.Quantity, product.QuantityAvailable + item.Quantity);
                }
            }

            // Roll the parent product's QuantityAvailable back up from its
            // variants so grid-level stock displays stay correct after a
            // variant-level decrement above.
            foreach (var productId in touchedProductIds)
            {
                var product = await db.Set<StoreProduct>().IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == productId);
                if (product is null) continue;
                var variants = await db.Set<StoreProductVariant>().IgnoreQueryFilters().Where(v => v.ProductId == productId).ToListAsync();
                product.QuantityAvailable = variants.Sum(v => v.QuantityAvailable);
            }
        }
        else
        {
            order.Status = "Failed";
            order.FailureMessage = $"Payment {paystackStatus}.";
        }

        await db.SaveChangesAsync();
    }

    public async Task<IApiResponse<StoreOrderStatusResponse>> GetOrderStatusAsync(string reference, AuthData member)
    {
        try
        {
            var order = await orderRepo.GetOneAsync(o => o.TransactionRef == reference);
            if (order is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<StoreOrderStatusResponse>("Order not found");
            if (order.MemberId != member.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreOrderStatusResponse>("Reference does not belong to the current member");

            if (order.Status == "Pending")
            {
                // Fallback in case the webhook hasn't landed yet.
                await ProcessReferenceAsync(reference);
                order = await orderRepo.GetOneAsync(o => o.TransactionRef == reference) ?? order;
            }

            return new StoreOrderStatusResponse
            {
                Reference = reference,
                Status = order.Status,
                Amount = order.TotalAmount,
                Message = order.Status switch
                {
                    "Successful" => "Payment confirmed",
                    "Pending" => "Payment has been initiated but not yet completed.",
                    _ => order.FailureMessage ?? "Payment failed",
                },
            }.ToOkApiResponse("Order status retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving store order status for reference {Reference}", reference);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreOrderStatusResponse>("Failed to retrieve order status");
        }
    }

    public async Task<IApiResponse<PgPagedResult<StoreOrderDto>>> GetMyOrdersAsync(StoreOrderFilter filter, string memberId)
    {
        try
        {
            var result = await orderRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                o => o.MemberId == memberId);

            var dtoResult = new PgPagedResult<StoreOrderDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(o => o.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving store orders for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<StoreOrderDto>>("Failed to retrieve orders");
        }
    }
}
