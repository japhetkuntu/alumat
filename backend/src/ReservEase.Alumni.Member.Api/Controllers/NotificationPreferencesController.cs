using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
public class NotificationPreferencesController(INotificationPreferenceService prefService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get notification preferences", Description = "Get the current member's notification preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<NotificationPreferenceDto>))]
    public async Task<IActionResult> GetPreferences()
    {
        var member = User.GetAccount();
        var result = await prefService.GetPreferencesAsync(member.Id);
        return result.ToActionResult();
    }

    [HttpPut]
    [SwaggerOperation(Summary = "Update notification preferences", Description = "Update the current member's notification preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<NotificationPreferenceDto>))]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferenceRequest request)
    {
        var member = User.GetAccount();
        var result = await prefService.UpdatePreferencesAsync(request, member.Id);
        return result.ToActionResult();
    }
}
