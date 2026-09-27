using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>Onboarding progress: which institutions are actively using the platform, and the lead → live funnel.</summary>
[Authorize(Roles = "SuperAdmin,Sales,Support")]
public class ActivationController(IActivationTrackingService activationService) : DefaultController
{
    [HttpGet("scorecard")]
    [SwaggerOperation(Summary = "Per-institution activation scorecard")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ActivationScorecardResponse>))]
    public async Task<IActionResult> GetScorecard()
    {
        var result = await activationService.GetScorecardAsync();
        return result.ToActionResult();
    }

    [HttpGet("institutions/{institutionId}")]
    [SwaggerOperation(Summary = "One institution's activation progress")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ActivationScorecardItem>))]
    public async Task<IActionResult> GetInstitution(string institutionId)
    {
        var result = await activationService.GetInstitutionAsync(institutionId);
        return result.ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin,Sales")]
    [HttpPut("institutions/{institutionId}/settings")]
    [SwaggerOperation(Summary = "Set an institution's member threshold and whether it gets setup reminders")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ActivationScorecardItem>))]
    public async Task<IActionResult> UpdateInstitutionSettings(string institutionId, [FromBody] UpdateInstitutionActivationSettingsRequest request)
    {
        var acct = User.GetAccount();
        var result = await activationService.UpdateInstitutionSettingsAsync(institutionId, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpGet("funnel")]
    [SwaggerOperation(Summary = "Onboarding funnel totals and week-by-week counts")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ActivationFunnelResponse>))]
    public async Task<IActionResult> GetFunnel([FromQuery] int weeks = 9)
    {
        var result = await activationService.GetFunnelAsync(weeks);
        return result.ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("target")]
    [SwaggerOperation(Summary = "Set or clear the activation goal (e.g. 20 institutions by a date)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ActivationScorecardResponse>))]
    public async Task<IActionResult> UpdateTarget([FromBody] UpdateActivationTargetRequest request)
    {
        var acct = User.GetAccount();
        var result = await activationService.UpdateTargetAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
