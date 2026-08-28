using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class StoreService(
    IAlumniPgRepository<StoreProduct> productRepo,
    IAlumniPgRepository<StoreOrder> orderRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    IStorageService storageService,
    ICurrentTenantService currentTenant,
    ILogger<StoreService> logger) : IStoreService
{
    public async Task<IApiResponse<PgPagedResult<StoreProductDto>>> GetProductsAsync(StoreProductFilter filter)
    {
        try
        {
            logger.LogInformation("GetProducts request — filter: {Filter}", filter.Serialize());
            var result = await productRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                p => (string.IsNullOrEmpty(filter.Status) || p.Status == filter.Status)
                  && (string.IsNullOrEmpty(filter.Search) || p.Name.Contains(filter.Search)));

            var dtoResult = new PgPagedResult<StoreProductDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(p => p.ToDto()).ToList(),
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
            if (product is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<StoreProductDto>("Product not found");

            return product.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving store product {ProductId}", productId);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreProductDto>("Failed to retrieve product");
        }
    }

    public async Task<IApiResponse<StoreProductDto>> CreateProductAsync(CreateStoreProductRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreateProduct request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);

            if (request.Price <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductDto>("Price must be greater than zero.");
            if (request.QuantityAvailable < 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductDto>("Quantity available cannot be negative.");

            // A blank DeliveryInfo means "use the institution's default" —
            // resolved once, here, and copied onto the product so every later
            // read (list, detail, checkout snapshot) sees a concrete value
            // without needing to know about the default at all.
            var deliveryInfo = request.DeliveryInfo;
            if (string.IsNullOrWhiteSpace(deliveryInfo) && !string.IsNullOrEmpty(currentTenant.InstitutionId))
            {
                var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
                deliveryInfo = institution?.DefaultStoreDeliveryInfo;
            }

            var product = new StoreProduct
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                QuantityAvailable = request.QuantityAvailable,
                DeliveryInfo = deliveryInfo,
                Status = request.Status,
                CreatedBy = admin.Id,
            };

            if (request.Images is { Count: > 0 })
                product.ImageUrls = await storageService.BulkUploadFilesAsync(request.Images, institutionSlug: currentTenant.InstitutionSlug ?? "");

            await productRepo.AddAsync(product);
            logger.LogInformation("Store product {ProductId} created by admin {AdminId}", product.Id, admin.Id);
            return product.ToDto().ToCreatedApiResponse("Product created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating store product: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreProductDto>("Failed to create product");
        }
    }

    public async Task<IApiResponse<StoreProductDto>> UpdateProductAsync(UpdateStoreProductRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdateProduct request for productId: {ProductId} by admin {AdminId}", request.ProductId, admin.Id);
            var product = await productRepo.GetByIdAsync(request.ProductId);
            if (product is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<StoreProductDto>("Product not found");

            if (request.Price <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductDto>("Price must be greater than zero.");
            if (request.QuantityAvailable < 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductDto>("Quantity available cannot be negative.");

            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.QuantityAvailable = request.QuantityAvailable;
            product.DeliveryInfo = request.DeliveryInfo;
            product.Status = request.Status;

            var imageUrls = new List<string>();
            if (request.ExistingImageUrls is { Count: > 0 })
                imageUrls.AddRange(request.ExistingImageUrls);
            if (request.Images is { Count: > 0 })
                imageUrls.AddRange(await storageService.BulkUploadFilesAsync(request.Images, institutionSlug: currentTenant.InstitutionSlug ?? ""));
            product.ImageUrls = imageUrls.Count > 0 ? imageUrls : null;

            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = admin.Id;
            await productRepo.UpdateAsync(product);
            logger.LogInformation("Store product {ProductId} updated by admin {AdminId}", product.Id, admin.Id);
            return product.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating store product {ProductId} by admin {AdminId}", request.ProductId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreProductDto>("Failed to update product");
        }
    }

    public async Task<IApiResponse<object>> DeleteProductAsync(string productId)
    {
        try
        {
            var product = await productRepo.GetByIdAsync(productId);
            if (product is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Product not found");

            await productRepo.RemoveAsync(product);
            logger.LogInformation("Store product {ProductId} deleted", productId);
            return new object().ToOkApiResponse("Product deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting store product {ProductId}", productId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete product");
        }
    }

    public async Task<IApiResponse<PgPagedResult<StoreOrderDto>>> GetOrdersAsync(StoreOrderFilter filter)
    {
        try
        {
            logger.LogInformation("GetOrders request — filter: {Filter}", filter.Serialize());
            // Institution staff only ever see fully paid orders — Pending/Failed
            // carry no obligation to fulfil and would just be noise (or worse,
            // get shipped before payment actually clears).
            var result = await orderRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                o => o.Status == "Confirmed");

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
            logger.LogError(e, "Error retrieving store orders — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<StoreOrderDto>>("Failed to retrieve orders");
        }
    }

    public async Task<IApiResponse<StoreSettingsResponse>> GetSettingsAsync()
    {
        var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        return new StoreSettingsResponse(institution?.DefaultStoreDeliveryInfo).ToOkApiResponse();
    }

    public async Task<IApiResponse<StoreSettingsResponse>> UpdateSettingsAsync(UpdateStoreSettingsRequest request, AuthData admin)
    {
        var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<StoreSettingsResponse>("Institution not found");

        institution.DefaultStoreDeliveryInfo = request.DefaultDeliveryInfo;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = admin.Id;
        await institutionRepo.UpdateAsync(institution);
        logger.LogInformation("Store default delivery info updated by admin {AdminId}", admin.Id);
        return new StoreSettingsResponse(institution.DefaultStoreDeliveryInfo).ToOkApiResponse("Default delivery info updated");
    }
}
