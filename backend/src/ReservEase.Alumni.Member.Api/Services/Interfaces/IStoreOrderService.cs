using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IStoreOrderService
{
    Task<IApiResponse<PgPagedResult<StoreProductDto>>> GetProductsAsync(StoreProductFilter filter);
    Task<IApiResponse<StoreProductDto>> GetProductByIdAsync(string productId);
    Task<IApiResponse<StoreCheckoutResponse>> InitiateCheckoutAsync(CheckoutRequest request, AuthData member);
    Task<IApiResponse<object>> ProcessPaystackCallbackAsync(string reference, string rawBody);
    Task<IApiResponse<StoreOrderStatusResponse>> GetOrderStatusAsync(string reference, AuthData member);
    Task<IApiResponse<PgPagedResult<StoreOrderDto>>> GetMyOrdersAsync(StoreOrderFilter filter, string memberId);
    /// <summary>Cheap existence check the Paystack webhook dispatcher uses to decide whether a reference belongs to a store order (vs a Contribution).</summary>
    Task<bool> OwnsReferenceAsync(string reference);
}
