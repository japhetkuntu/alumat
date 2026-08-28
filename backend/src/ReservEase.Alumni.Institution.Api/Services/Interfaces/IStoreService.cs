using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IStoreService
{
    Task<IApiResponse<PgPagedResult<StoreProductDto>>> GetProductsAsync(StoreProductFilter filter);
    Task<IApiResponse<StoreProductDto>> GetProductByIdAsync(string productId);
    Task<IApiResponse<StoreProductDto>> CreateProductAsync(CreateStoreProductRequest request, AuthData admin);
    Task<IApiResponse<StoreProductDto>> UpdateProductAsync(UpdateStoreProductRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteProductAsync(string productId);
    Task<IApiResponse<PgPagedResult<StoreOrderDto>>> GetOrdersAsync(StoreOrderFilter filter);
    Task<IApiResponse<StoreSettingsResponse>> GetSettingsAsync();
    Task<IApiResponse<StoreSettingsResponse>> UpdateSettingsAsync(UpdateStoreSettingsRequest request, AuthData admin);
}
