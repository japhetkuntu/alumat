using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage financial contributions to campaigns.
/// </summary>
[Authorize]
// No controller-level [RequireFeature] here — Contributions (online) and
// ManualPayments (offline) are independently toggle-able, so each action is
// gated individually below rather than sharing one class-level key.
public class ContributionsController(IContributionService contributionService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of contributions with optional filters.
    /// </summary>
    [HttpGet]
    [RequireFeature(InstitutionFeatures.Contributions)]
    [SwaggerOperation(Summary = "List contributions", Description = "Filter by campaignId and/or status.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ContributionDto>>))]
    public async Task<IActionResult> GetContributions([FromQuery] ContributionInstitutionStaffFilter filter)
    {
        var admin = User.GetAccount();
        var result = await contributionService.GetContributionsAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Record a manual (offline) contribution.
    /// </summary>
    [HttpPost("manual")]
    [RequireFeature(InstitutionFeatures.ManualPayments)]
    [SwaggerOperation(Summary = "Record manual contribution")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<ContributionDto>))]
    public async Task<IActionResult> RecordManualContribution([FromBody] RecordManualContributionRequest request)
    {
        var admin = User.GetAccount();
        var result = await contributionService.RecordManualContributionAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Confirm a pending contribution.
    /// </summary>
    [HttpPut("{contributionId}/confirm")]
    [RequireFeature(InstitutionFeatures.Contributions)]
    [SwaggerOperation(Summary = "Confirm contribution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ConfirmContribution(string contributionId)
    {
        var admin = User.GetAccount();
        var result = await contributionService.ConfirmContributionAsync(contributionId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Reject a contribution with an optional reason.
    /// </summary>
    [HttpPut("{contributionId}/reject")]
    [RequireFeature(InstitutionFeatures.Contributions)]
    [SwaggerOperation(Summary = "Reject contribution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RejectContribution(string contributionId, [FromBody] RejectContributionRequest request)
    {
        var admin = User.GetAccount();
        var result = await contributionService.RejectContributionAsync(contributionId, request.Reason, admin);
        return result.ToActionResult();
    }
}
