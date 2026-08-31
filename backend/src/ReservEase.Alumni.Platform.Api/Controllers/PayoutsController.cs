using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Estimated Paystack settlement across every institution — same figures
/// each institution's own SuperAdmins see on their side (Institution.Api's
/// PayoutsController), just totaled and broken out per institution here.
/// </summary>
[Authorize(Roles = "SuperAdmin,Billing")]
public class PayoutsController(IPayoutService payoutService) : DefaultController
{
    /// <summary>Estimated last and next Paystack settlement, all institutions.</summary>
    [HttpGet("forecast")]
    [SwaggerOperation(Summary = "Get payout forecast across all institutions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformPayoutForecastResponse>))]
    public async Task<IActionResult> GetForecast()
    {
        var result = await payoutService.GetForecastAsync();
        return result.ToActionResult();
    }
}
