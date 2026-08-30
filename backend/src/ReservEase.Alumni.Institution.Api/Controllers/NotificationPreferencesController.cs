using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;

namespace ReservEase.Alumni.Institution.Api.Controllers;

[Authorize]
public class NotificationPreferencesController(INotificationPreferenceService prefService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get notification preferences", Description = "Get the current staff member's notification preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AdminNotificationPreferenceDto>))]
    public async Task<IActionResult> GetPreferences()
    {
        var admin = User.GetAccount();
        var result = await prefService.GetPreferencesAsync(admin.Id);
        return result.ToActionResult();
    }

    [HttpPut]
    [SwaggerOperation(Summary = "Update notification preferences", Description = "Update the current staff member's notification preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AdminNotificationPreferenceDto>))]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateAdminNotificationPreferenceRequest request)
    {
        var admin = User.GetAccount();
        var result = await prefService.UpdatePreferencesAsync(request, admin.Id);
        return result.ToActionResult();
    }
}
