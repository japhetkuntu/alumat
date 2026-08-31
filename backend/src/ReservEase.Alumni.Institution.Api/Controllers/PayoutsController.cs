using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// What this institution should expect from Paystack's next settlement —
/// SuperAdmin-only, since it's the figure the institution reconciles against
/// its own bank account.
/// </summary>
[Authorize(Roles = StaffRoles.SuperAdmin)]
public class PayoutsController(IPayoutService payoutService) : DefaultController
{
    /// <summary>Estimated last and next Paystack settlement for this institution.</summary>
    [HttpGet("forecast")]
    [SwaggerOperation(Summary = "Get payout forecast")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PayoutForecastResponse>))]
    public async Task<IActionResult> GetForecast()
    {
        var result = await payoutService.GetForecastAsync();
        return result.ToActionResult();
    }
}
