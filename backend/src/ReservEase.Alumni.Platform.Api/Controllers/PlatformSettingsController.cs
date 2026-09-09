using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>Platform-wide behavior toggles — apply across every institution at once, unlike per-institution feature flags (see FeaturesController).</summary>
[Authorize]
[Route("api/v{version:apiVersion}/platform-settings")]
public class PlatformSettingsController(IPlatformSettingsService settingsService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get platform-wide settings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformSettingsResponse>))]
    public async Task<IActionResult> Get()
    {
        var result = await settingsService.GetAsync();
        return result.ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPatch]
    [SwaggerOperation(Summary = "Update platform-wide settings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformSettingsResponse>))]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformSettingsRequest request)
    {
        var admin = User.GetAccount();
        var result = await settingsService.UpdateAsync(request, admin.Id, $"{admin.FirstName} {admin.LastName}".Trim());
        return result.ToActionResult();
    }
}
