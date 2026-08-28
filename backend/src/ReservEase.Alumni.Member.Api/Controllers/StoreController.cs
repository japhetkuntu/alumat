using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Browse store products and check out — same platform-fee model as Contributions.</summary>
[Authorize]
[RequireFeature(InstitutionFeatures.Store)]
public class StoreController(IStoreOrderService storeOrderService) : DefaultController
{
    [HttpGet("products")]
    [SwaggerOperation(Summary = "List store products")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<StoreProductDto>>))]
    public async Task<IActionResult> GetProducts([FromQuery] StoreProductFilter filter)
    {
        var result = await storeOrderService.GetProductsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("products/{productId}")]
    [SwaggerOperation(Summary = "Get store product by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreProductDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetProduct(string productId)
    {
        var result = await storeOrderService.GetProductByIdAsync(productId);
        return result.ToActionResult();
    }

    [HttpPost("checkout")]
    [SwaggerOperation(Summary = "Initiate checkout for a cart of products")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreCheckoutResponse>))]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
    {
        var member = User.GetAccount();
        var result = await storeOrderService.InitiateCheckoutAsync(request, member);
        return result.ToActionResult();
    }

    [HttpGet("orders/{reference}/status")]
    [SwaggerOperation(Summary = "Poll an order's payment status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreOrderStatusResponse>))]
    public async Task<IActionResult> GetOrderStatus(string reference)
    {
        var member = User.GetAccount();
        var result = await storeOrderService.GetOrderStatusAsync(reference, member);
        return result.ToActionResult();
    }

    [HttpGet("orders")]
    [SwaggerOperation(Summary = "List my store orders")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<StoreOrderDto>>))]
    public async Task<IActionResult> GetMyOrders([FromQuery] StoreOrderFilter filter)
    {
        var member = User.GetAccount();
        var result = await storeOrderService.GetMyOrdersAsync(filter, member.Id);
        return result.ToActionResult();
    }
}
