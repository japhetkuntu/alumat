using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

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
}
