using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;

namespace Umat.Alumni.Member.Api.Controllers;

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
