using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class StoreService(
    IAlumniPgRepository<StoreProduct> productRepo,
    IAlumniPgRepository<StoreOrder> orderRepo,
    IAlumniPgRepository<StoreProductVariant> variantRepo,
    IAlumniPgRepository<StoreProductTemplate> templateRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    IAlumniPgRepository<Member> memberRepo,
    IStorageService storageService,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    ILogger<StoreService> logger) : IStoreService
{
    /// <summary>
    /// Replaces the full variant set for a product (delete-existing-and-recreate,
    /// matching the "admin submits the whole variant table each edit" UX — no
    /// per-variant diffing/ID preservation across an edit) and rolls the
    /// parent product's Price/QuantityAvailable up from the submitted variants:
    /// Price becomes the lowest effective price (PriceOverride ?? submitted
    /// product Price), QuantityAvailable becomes the sum of variant quantities.
    /// When <paramref name="variantRequests"/> is empty, the product stays (or
    /// becomes) a simple product and its Price/QuantityAvailable are left as
    /// already set by the caller.
    /// </summary>
    private async Task<List<StoreProductVariant>> ReplaceVariantsAsync(StoreProduct product, List<string> variantOptionTypes, List<VariantRequest> variantRequests, string adminId)
    {
        var existing = (await variantRepo.GetAllAsync(v => v.ProductId == product.Id)).ToList();
        if (existing.Count > 0)
        {
            foreach (var old in existing)
                await variantRepo.RemoveAsync(old);
        }

        product.VariantOptionTypes = variantOptionTypes;

        if (variantRequests is not { Count: > 0 })
        {
            return [];
        }

        var newVariants = variantRequests.Select(r => new StoreProductVariant
        {
            ProductId = product.Id,
            Options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(r.OptionsJson) ?? new(),
            Sku = r.Sku,
            PriceOverride = r.PriceOverride,
            QuantityAvailable = r.QuantityAvailable,
            CreatedBy = adminId,
        }).ToList();

        await variantRepo.AddRangeAsync(newVariants);

        product.Price = newVariants.Min(v => v.PriceOverride ?? product.Price);
        product.QuantityAvailable = newVariants.Sum(v => v.QuantityAvailable);

        return newVariants;
    }

    /// <summary>
    /// Applies the submitted display/question/delivery/stage configuration to a product. A null JSON
    /// string means "leave that part unchanged" (update only); returns an error message when anything
    /// submitted is invalid, in which case the product must not be saved.
    /// </summary>
    private static string? ApplyProductConfig(StoreProduct product, string? priceLabel, bool trackStock, string? detailsJson, string? fieldsJson, string? deliveryFieldsJson, string? stagesJson)
    {
        product.PriceLabel = string.IsNullOrWhiteSpace(priceLabel) ? null : priceLabel.Trim();
        product.TrackStock = trackStock;

        if (detailsJson is not null)
        {
            var (details, error) = StoreConfigParser.ParseJson<StoreDetailItemRequest>(detailsJson, "product details");
            if (error is not null) return error;
            product.Details = StoreConfigParser.MapDetails(details);
        }
        if (fieldsJson is not null)
        {
            var (raw, error) = StoreConfigParser.ParseJson<ServiceFieldDefinitionRequest>(fieldsJson, "order questions");
            if (error is not null) return error;
            var (fields, mapError) = StoreConfigParser.MapFields(raw!, "order");
            if (mapError is not null) return mapError;
            product.Fields = fields!;
        }
        if (deliveryFieldsJson is not null)
        {
            var (raw, error) = StoreConfigParser.ParseJson<ServiceFieldDefinitionRequest>(deliveryFieldsJson, "delivery questions");
            if (error is not null) return error;
            var (fields, mapError) = StoreConfigParser.MapFields(raw!, "delivery");
            if (mapError is not null) return mapError;
            product.DeliveryFields = fields!;
        }
        if (stagesJson is not null)
        {
            var (stages, error) = StoreConfigParser.ParseJson<string>(stagesJson, "stages");
            if (error is not null) return error;
            product.Stages = StoreConfigParser.NormalizeStages(stages);
        }
        return null;
    }

    public async Task<IApiResponse<PgPagedResult<StoreProductDto>>> GetProductsAsync(StoreProductFilter filter)
    {
        try
        {
            logger.LogInformation("GetProducts request — filter: {Filter}", filter.Serialize());
            var search = filter.Search?.ToLower();
            var result = await productRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                p => (string.IsNullOrEmpty(filter.Status) || p.Status == filter.Status)
                  && TextSearch.Matches(search, p.Name));

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
            if (product is null)
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

            var configError = ApplyProductConfig(product, request.PriceLabel, request.TrackStock, request.DetailsJson, request.FieldsJson, request.DeliveryFieldsJson, request.StagesJson);
            if (configError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductDto>(configError);

            if (request.Images is { Count: > 0 })
                product.ImageUrls = await storageService.BulkUploadFilesAsync(request.Images, institutionSlug: currentTenant.InstitutionSlug ?? "");

            await productRepo.AddAsync(product);

            List<StoreProductVariant> variants = [];
            if (request.VariantOptionTypes is { Count: > 0 })
            {
                variants = await ReplaceVariantsAsync(product, request.VariantOptionTypes, request.Variants, admin.Id);
                await productRepo.UpdateAsync(product);
            }

            logger.LogInformation("Store product {ProductId} created by admin {AdminId}", product.Id, admin.Id);
            return product.ToDto(variants).ToCreatedApiResponse("Product created");
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

            var configError = ApplyProductConfig(product, request.PriceLabel, request.TrackStock, request.DetailsJson, request.FieldsJson, request.DeliveryFieldsJson, request.StagesJson);
            if (configError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductDto>(configError);

            var imageUrls = new List<string>();
            if (request.ExistingImageUrls is { Count: > 0 })
                imageUrls.AddRange(request.ExistingImageUrls);
            if (request.Images is { Count: > 0 })
                imageUrls.AddRange(await storageService.BulkUploadFilesAsync(request.Images, institutionSlug: currentTenant.InstitutionSlug ?? ""));
            product.ImageUrls = imageUrls.Count > 0 ? imageUrls : null;

            // Variants are always replaced wholesale from what's submitted this
            // edit — an empty VariantOptionTypes clears any existing variants
            // and reverts the product to simple (Price/QuantityAvailable above
            // stay as the admin set them directly).
            var variants = await ReplaceVariantsAsync(product, request.VariantOptionTypes, request.Variants, admin.Id);

            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = admin.Id;
            await productRepo.UpdateAsync(product);
            logger.LogInformation("Store product {ProductId} updated by admin {AdminId}", product.Id, admin.Id);
            return product.ToDto(variants).ToOkApiResponse();
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

            var variants = (await variantRepo.GetAllAsync(v => v.ProductId == productId)).ToList();
            foreach (var variant in variants)
                await variantRepo.RemoveAsync(variant);

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
                o => o.Status == "Successful"
                  && (string.IsNullOrEmpty(filter.DeliveryStatus) || o.DeliveryStatus == filter.DeliveryStatus));

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

            // ToDto() reads name/email from the MemberSnapshot frozen on the order at
            // creation time — refresh both from the live Member records here (one
            // batched lookup, not one query per row) so a later name/email change
            // actually shows up. Falls back to the snapshot if the member has since
            // been deleted.
            var orderMemberIds = dtoResult.Results.Select(d => d.MemberId).Distinct().ToList();
            var orderMembers = (await memberRepo.GetAllAsync(m => orderMemberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtoResult.Results)
            {
                if (orderMembers.TryGetValue(dto.MemberId, out var m))
                {
                    dto.MemberName = $"{m.FirstName} {m.LastName}";
                    dto.MemberEmail = m.Email;
                }
            }

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
        try
        {
        var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        return new StoreSettingsResponse(institution?.DefaultStoreDeliveryInfo, institution?.StoreDeliveryStages ?? []).ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetSettingsAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreSettingsResponse>("Failed to getsettings");
        }}

    public async Task<IApiResponse<StoreSettingsResponse>> UpdateSettingsAsync(UpdateStoreSettingsRequest request, AuthData admin)
    {
        try
        {
        var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<StoreSettingsResponse>("Institution not found");

        institution.DefaultStoreDeliveryInfo = request.DefaultDeliveryInfo;
        if (request.DeliveryStages is not null)
            institution.StoreDeliveryStages = request.DeliveryStages;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = admin.Id;
        await institutionRepo.UpdateAsync(institution);
        logger.LogInformation("Store settings updated by admin {AdminId}", admin.Id);
        return new StoreSettingsResponse(institution.DefaultStoreDeliveryInfo, institution.StoreDeliveryStages).ToOkApiResponse("Store settings updated");

        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateSettingsAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreSettingsResponse>("Failed to updatesettings");
        }}

    public async Task<IApiResponse<StoreOrderDto>> UpdateDeliveryStatusAsync(string orderId, string? newStatus, AuthData admin)
    {
        try
        {
            var order = await orderRepo.GetByIdAsync(orderId);
            if (order is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<StoreOrderDto>("Order not found");

            if (newStatus is not null)
            {
                var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
                var validStages = institution?.StoreDeliveryStages ?? [];
                if (!validStages.Contains(newStatus))
                    return ApiResponseExtensions.ToBadRequestApiResponse<StoreOrderDto>($"\"{newStatus}\" is not one of this store's configured delivery stages.");

                order.DeliveryStatusHistory.Add(new StoreOrderDeliveryEvent { Status = newStatus, ChangedAt = DateTime.UtcNow });
            }

            order.DeliveryStatus = newStatus;
            order.DeliveryStatusUpdatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = admin.Id;
            await orderRepo.UpdateAsync(order);

            if (!string.IsNullOrEmpty(newStatus))
            {
                await temporalProvider.EnqueueNotificationAsync(NotificationRequest.StoreDeliveryStatusUpdated(
                    currentTenant.InstitutionId!, order.MemberId, order.Id, order.OrderNumber, newStatus), logger);
            }

            logger.LogInformation("Store order {OrderId} delivery status set to {DeliveryStatus} by admin {AdminId}", orderId, newStatus, admin.Id);
            return order.ToDto().ToOkApiResponse("Delivery status updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating delivery status for store order {OrderId} by admin {AdminId}", orderId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreOrderDto>("Failed to update delivery status");
        }
    }

    // ── Reusable setups ────────────────────────────────────────────────────

    public async Task<IApiResponse<List<StoreProductTemplateDto>>> GetTemplatesAsync()
    {
        try
        {
            var templates = (await templateRepo.GetAllAsync()).OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
            return templates.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving store templates");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<StoreProductTemplateDto>>("Failed to retrieve templates");
        }
    }

    private static string? ApplyTemplate(StoreProductTemplate template, StoreProductTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Give this setup a name.";
        var (fields, fieldError) = StoreConfigParser.MapFields(request.Fields, "order");
        if (fieldError is not null) return fieldError;
        var (deliveryFields, deliveryError) = StoreConfigParser.MapFields(request.DeliveryFields, "delivery");
        if (deliveryError is not null) return deliveryError;

        template.Name = request.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        template.PriceLabel = string.IsNullOrWhiteSpace(request.PriceLabel) ? null : request.PriceLabel.Trim();
        template.TrackStock = request.TrackStock;
        template.DeliveryInfo = string.IsNullOrWhiteSpace(request.DeliveryInfo) ? null : request.DeliveryInfo.Trim();
        template.Details = StoreConfigParser.MapDetails(request.Details);
        template.Fields = fields!;
        template.DeliveryFields = deliveryFields!;
        template.Stages = StoreConfigParser.NormalizeStages(request.Stages);
        return null;
    }

    public async Task<IApiResponse<StoreProductTemplateDto>> CreateTemplateAsync(StoreProductTemplateRequest request, AuthData admin)
    {
        try
        {
            var template = new StoreProductTemplate { CreatedBy = admin.Id };
            var error = ApplyTemplate(template, request);
            if (error is not null) return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductTemplateDto>(error);
            await templateRepo.AddAsync(template);
            return template.ToDto().ToCreatedApiResponse("Setup saved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating store template by admin {AdminId}", admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreProductTemplateDto>("Failed to save setup");
        }
    }

    public async Task<IApiResponse<StoreProductTemplateDto>> UpdateTemplateAsync(string templateId, StoreProductTemplateRequest request, AuthData admin)
    {
        try
        {
            var template = await templateRepo.GetByIdAsync(templateId);
            if (template is null) return ApiResponseExtensions.ToNotFoundApiResponse<StoreProductTemplateDto>("Setup not found");
            var error = ApplyTemplate(template, request);
            if (error is not null) return ApiResponseExtensions.ToBadRequestApiResponse<StoreProductTemplateDto>(error);
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = admin.Id;
            await templateRepo.UpdateAsync(template);
            return template.ToDto().ToOkApiResponse("Setup updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating store template {TemplateId} by admin {AdminId}", templateId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreProductTemplateDto>("Failed to update setup");
        }
    }

    public async Task<IApiResponse<object>> DeleteTemplateAsync(string templateId)
    {
        try
        {
            var template = await templateRepo.GetByIdAsync(templateId);
            if (template is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Setup not found");
            // Products were configured from a copy, so removing the template never affects them.
            await templateRepo.RemoveAsync(template);
            return new object().ToOkApiResponse("Setup deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting store template {TemplateId}", templateId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete setup");
        }
    }

    // ── Per-item fulfilment ────────────────────────────────────────────────

    public async Task<IApiResponse<StoreOrderDto>> UpdateOrderItemAsync(string orderId, int itemIndex, UpdateStoreOrderItemRequest request, AuthData admin)
    {
        try
        {
            var order = await orderRepo.GetByIdAsync(orderId);
            if (order is null || itemIndex < 0 || itemIndex >= order.Items.Count)
                return ApiResponseExtensions.ToNotFoundApiResponse<StoreOrderDto>("Order item not found");
            var item = order.Items[itemIndex];

            if (item.Stages.Count == 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreOrderDto>("This item has no stages of its own; update the order's delivery status instead.");
            if (string.IsNullOrWhiteSpace(request.Stage) && string.IsNullOrWhiteSpace(request.Note) && request.Attachment is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreOrderDto>("Provide a stage, a note, or an attachment");
            if (!string.IsNullOrWhiteSpace(request.Stage) && !item.Stages.Contains(request.Stage))
                return ApiResponseExtensions.ToBadRequestApiResponse<StoreOrderDto>($"\"{request.Stage}\" is not one of this item's stages.");

            string? attachmentUrl = null;
            if (request.Attachment is { Length: > 0 })
            {
                var objectName = $"{Guid.NewGuid():N}{Path.GetExtension(request.Attachment.FileName)}";
                attachmentUrl = await storageService.UploadFileAsync(request.Attachment, objectName, "alumni", currentTenant.InstitutionSlug ?? "");
            }

            item.Updates.Add(new ServiceRequestUpdate
            {
                ChangedAt = DateTime.UtcNow,
                Stage = string.IsNullOrWhiteSpace(request.Stage) ? null : request.Stage,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                AttachmentUrl = attachmentUrl,
                ChangedByStaffName = $"{admin.FirstName} {admin.LastName}",
            });
            if (!string.IsNullOrWhiteSpace(request.Stage))
                item.CurrentStage = request.Stage;

            // Reassign so EF notices the change inside the jsonb Items list.
            order.Items = [.. order.Items];
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = admin.Id;
            await orderRepo.UpdateAsync(order);

            await temporalProvider.EnqueueNotificationAsync(NotificationRequest.StoreDeliveryStatusUpdated(
                currentTenant.InstitutionId!, order.MemberId, order.Id, order.OrderNumber,
                $"{item.ProductName}: {item.CurrentStage}"), logger);

            logger.LogInformation("Store order {OrderId} item {ItemIndex} updated by admin {AdminId} — stage={Stage}", orderId, itemIndex, admin.Id, request.Stage);
            return order.ToDto().ToOkApiResponse("Item updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating item {ItemIndex} of store order {OrderId}", itemIndex, orderId);
            return ApiResponseExtensions.ToServerErrorApiResponse<StoreOrderDto>("Failed to update item");
        }
    }
}
