using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/dashboard")]
public class DashboardController(IInstitutionManagementService institutionService) : DefaultController
{
    [HttpGet("summary")]
    [SwaggerOperation(Summary = "Platform-wide aggregate stats")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformDashboardSummary>))]
    public async Task<IActionResult> GetSummary()
    {
        var result = await institutionService.GetDashboardSummaryAsync();
        return result.ToActionResult();
    }

    /// <summary>Every payment across every institution — Contributions and Store orders, every status — for platform-wide analytics and troubleshooting.</summary>
    [HttpGet("payments")]
    [SwaggerOperation(Summary = "Get all payments across every institution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<PlatformPaymentDto>>))]
    public async Task<IActionResult> GetPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] string? source = null)
    {
        var result = await institutionService.GetPaymentsAsync(null, page, pageSize, status, source);
        return result.ToActionResult();
    }
}
