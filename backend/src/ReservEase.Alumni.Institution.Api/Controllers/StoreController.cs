using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage store products and view orders — SuperAdmin only, per the
/// institution's own choice of who can list merchandise and see who bought
/// what. Delivery is handled by staff outside the platform; this is
/// inventory + listing management only.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[RequireFeature(InstitutionFeatures.Store)]
public class StoreController(IStoreService storeService) : DefaultController
{
    [HttpGet("products")]
    [SwaggerOperation(Summary = "List store products")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<StoreProductDto>>))]
    public async Task<IActionResult> GetProducts([FromQuery] StoreProductFilter filter)
    {
        var result = await storeService.GetProductsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("products/{productId}")]
    [SwaggerOperation(Summary = "Get store product by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreProductDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetProduct(string productId)
    {
        var result = await storeService.GetProductByIdAsync(productId);
        return result.ToActionResult();
    }

    [HttpPost("products")]
    [SwaggerOperation(Summary = "Create store product")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<StoreProductDto>))]
    public async Task<IActionResult> CreateProduct([FromForm] CreateStoreProductRequest request)
    {
        var admin = User.GetAccount();
        var result = await storeService.CreateProductAsync(request, admin);
        return result.ToActionResult();
    }

    [HttpPut("products/{productId}")]
    [SwaggerOperation(Summary = "Update store product")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreProductDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateProduct(string productId, [FromForm] UpdateStoreProductRequest request)
    {
        request.ProductId = productId;
        var admin = User.GetAccount();
        var result = await storeService.UpdateProductAsync(request, admin);
        return result.ToActionResult();
    }

    [HttpDelete("products/{productId}")]
    [SwaggerOperation(Summary = "Delete store product")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteProduct(string productId)
    {
        var result = await storeService.DeleteProductAsync(productId);
        return result.ToActionResult();
    }

    [HttpGet("orders")]
    [SwaggerOperation(Summary = "List store orders", Description = "So staff can see what to fulfill — delivery itself happens outside the platform.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<StoreOrderDto>>))]
    public async Task<IActionResult> GetOrders([FromQuery] StoreOrderFilter filter)
    {
        var result = await storeService.GetOrdersAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("settings")]
    [SwaggerOperation(Summary = "Get store settings", Description = "The default delivery info applied to new products left blank.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreSettingsResponse>))]
    public async Task<IActionResult> GetSettings()
    {
        var result = await storeService.GetSettingsAsync();
        return result.ToActionResult();
    }

    [HttpPatch("settings")]
    [SwaggerOperation(Summary = "Update store settings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreSettingsResponse>))]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateStoreSettingsRequest request)
    {
        var admin = User.GetAccount();
        var result = await storeService.UpdateSettingsAsync(request, admin);
        return result.ToActionResult();
    }

    [HttpPatch("orders/{orderId}/delivery-status")]
    [SwaggerOperation(Summary = "Update an order's delivery status", Description = "Status must be one of the institution's configured Store delivery stages, or null to clear tracking.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreOrderDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateDeliveryStatus(string orderId, [FromBody] UpdateStoreOrderDeliveryStatusRequest request)
    {
        var admin = User.GetAccount();
        var result = await storeService.UpdateDeliveryStatusAsync(orderId, request.DeliveryStatus, admin);
        return result.ToActionResult();
    }
}
