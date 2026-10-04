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
public class DashboardController(IInstitutionManagementService institutionService, IPlatformAnalyticsService analyticsService) : DefaultController
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
    [Authorize(Roles = "SuperAdmin,Billing,Support")]
    [HttpGet("payments")]
    [SwaggerOperation(Summary = "Get all payments across every institution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<PlatformPaymentDto>>))]
    public async Task<IActionResult> GetPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] string? source = null)
    {
        var result = await institutionService.GetPaymentsAsync(null, page, pageSize, status, source);
        return result.ToActionResult();
    }

    /// <summary>The latest N months (default 6) of successful payments by source, plus payment counts by status, for the Payments &amp; Revenue charts. Exact at any volume; full history is in the payments list.</summary>
    [Authorize(Roles = "SuperAdmin,Billing,Support")]
    [HttpGet("revenue-trend")]
    [SwaggerOperation(Summary = "Paid revenue by month and source, across every institution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformRevenueTrendDto>))]
    public async Task<IActionResult> GetRevenueTrend([FromQuery] int months = 6)
    {
        var result = await institutionService.GetRevenueTrendAsync(months);
        return result.ToActionResult();
    }

    /// <summary>Growth, participation, money and the institutions that lead or have gone quiet. Revenue is included only for roles that already see the payments list.</summary>
    [HttpGet("analytics")]
    [SwaggerOperation(Summary = "Platform analytics")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformAnalyticsDto>))]
    public async Task<IActionResult> GetAnalytics()
    {
        var includeMoney = User.IsInRole("SuperAdmin") || User.IsInRole("Billing") || User.IsInRole("Support");
        var result = await analyticsService.GetAnalyticsAsync(includeMoney);
        return result.ToActionResult();
    }
}
