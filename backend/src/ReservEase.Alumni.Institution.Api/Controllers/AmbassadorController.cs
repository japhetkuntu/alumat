using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Engagement;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// The year-group ambassador's own workspace. An ambassador is a scoped administrator assigned to year groups (managed on the
/// Institution Admins page); nothing here widens what they could already reach, and every figure is cut to their year groups.
/// </summary>
[Authorize(Roles = "ScopedAdmin")]
public class AmbassadorController(IEngagementService engagement) : DefaultController
{
    [HttpGet("workspace")]
    [SwaggerOperation(Summary = "My year groups", Description = "Cohort figures, the newest members, and tasks delegated to me. Names only: no contact details.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AmbassadorWorkspaceDto>))]
    public async Task<IActionResult> GetWorkspace([FromQuery] int days = 30)
    {
        var institution = HttpContext.Items["Institution"] as PostgresDb.Sdk.Entities.Institution;
        return (await engagement.GetAmbassadorWorkspaceAsync(User.GetAccount(), days, institution?.DisabledFeatures ?? [], EngagementController.UsesYearGroups(institution))).ToActionResult();
    }

    [HttpPost("tasks/{id}/{outcome}")]
    [SwaggerOperation(Summary = "Complete or postpone a task delegated to me", Description = "outcome is complete or snooze")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<RecommendationDto>))]
    public async Task<IActionResult> UpdateTask(string id, string outcome)
        => (await engagement.UpdateMyTaskAsync(User.GetAccount(), id, outcome)).ToActionResult();
}
