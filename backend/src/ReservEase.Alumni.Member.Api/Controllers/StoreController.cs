using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
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
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "Initiate checkout for a cart of products")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreCheckoutResponse>))]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
    {
        var member = User.GetAccount();
        var result = await storeOrderService.InitiateCheckoutAsync(request, member);
        return result.ToActionResult();
    }

    /// <summary>The same checkout, for carts with file questions: the cart is sent as CartJson and each file as its own form part named "{itemIndex}:{details|delivery}:{questionKey}".</summary>
    [HttpPost("checkout")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    [SwaggerOperation(Summary = "Initiate checkout for a cart that includes file uploads")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StoreCheckoutResponse>))]
    public async Task<IActionResult> CheckoutWithFiles([FromForm] string cartJson)
    {
        CheckoutRequest? request;
        try { request = System.Text.Json.JsonSerializer.Deserialize<CheckoutRequest>(cartJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); }
        catch (System.Text.Json.JsonException) { request = null; }
        if (request is null)
            return BadRequest(new { message = "Could not read your cart." });

        var files = Request.Form.Files.ToDictionary(f => f.Name, f => f);
        var result = await storeOrderService.InitiateCheckoutAsync(request, User.GetAccount(), files);
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
