using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Engagement;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// The engagement workspace: how healthy the community is and what to do about it. Super administrators only for now. A
/// year-group or community administrator would need these figures cut to their scope, which arrives with ambassadors.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
public class EngagementController(IEngagementService engagement) : DefaultController
{
    [HttpGet("dashboard")]
    [SwaggerOperation(Summary = "Engagement dashboard", Description = "Health score, participation, recommended actions and this week's checklist for a 7, 30 or 90 day period")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EngagementDashboardDto>))]
    public async Task<IActionResult> GetDashboard([FromQuery] int days = 30)
    {
        var institution = HttpContext.Items["Institution"] as PostgresDb.Sdk.Entities.Institution;
        var result = await engagement.GetDashboardAsync(User.GetAccount(), days, institution?.DisabledFeatures ?? [], UsesYearGroups(institution));
        return result.ToActionResult();
    }

    [HttpGet("monthly")]
    [SwaggerOperation(Summary = "Monthly engagement report", Description = "One calendar month against the month before: activity and outcomes kept apart, money shown as collected, fees deducted and net")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MonthlyReportDto>))]
    public async Task<IActionResult> GetMonthly([FromQuery] string? month = null)
    {
        var institution = HttpContext.Items["Institution"] as PostgresDb.Sdk.Entities.Institution;
        return (await engagement.GetMonthlyReportAsync(User.GetAccount(), month, institution?.DisabledFeatures ?? [], UsesYearGroups(institution))).ToActionResult();
    }

    [HttpGet("cohorts")]
    [SwaggerOperation(Summary = "Year groups and their ambassadors", Description = "Members, activation, participation, invitations and ambassador cover per year group")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CohortsDto>))]
    public async Task<IActionResult> GetCohorts([FromQuery] int days = 30)
    {
        var institution = HttpContext.Items["Institution"] as PostgresDb.Sdk.Entities.Institution;
        return (await engagement.GetCohortsAsync(User.GetAccount(), days, institution?.DisabledFeatures ?? [], UsesYearGroups(institution))).ToActionResult();
    }

    internal static bool UsesYearGroups(PostgresDb.Sdk.Entities.Institution? institution) => institution?.OrganizationType != "Community";

    [HttpGet("health/history")]
    [SwaggerOperation(Summary = "Health score history", Description = "Daily readings, oldest first, each with the factors that produced it")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IReadOnlyList<HealthSnapshotDto>>))]
    public async Task<IActionResult> GetHealthHistory([FromQuery] int days = 30, [FromQuery] int limit = 60)
        => (await engagement.GetHealthHistoryAsync(days, limit)).ToActionResult();

    [HttpGet("recommendations")]
    [SwaggerOperation(Summary = "Recommendation history", Description = "Every recommendation, newest first; filter by status (Open, Completed, Dismissed, Snoozed, Expired)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<RecommendationDto>>))]
    public async Task<IActionResult> ListRecommendations([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => (await engagement.ListRecommendationsAsync(status, page, pageSize)).ToActionResult();

    [HttpPost("recommendations/{id}/resolve")]
    [SwaggerOperation(Summary = "Complete, dismiss or snooze a recommendation")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<RecommendationDto>))]
    public async Task<IActionResult> Resolve(string id, [FromBody] ResolveRecommendationRequest request)
        => (await engagement.ResolveRecommendationAsync(User.GetAccount(), id, request.Action, request.SnoozeDays)).ToActionResult();

    [HttpPost("recommendations/{id}/assign")]
    [SwaggerOperation(Summary = "Delegate a recommendation to another administrator", Description = "Send no staffId to clear the assignment")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<RecommendationDto>))]
    public async Task<IActionResult> Assign(string id, [FromBody] AssignRecommendationRequest request)
        => (await engagement.AssignRecommendationAsync(User.GetAccount(), id, request.StaffId)).ToActionResult();

    [HttpPut("checklist/{itemKey}")]
    [SwaggerOperation(Summary = "Tick or untick a weekly checklist item")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SetChecklistItem(string itemKey, [FromBody] ChecklistToggleRequest request)
        => (await engagement.SetChecklistItemAsync(User.GetAccount(), itemKey, request.Done)).ToActionResult();
}
