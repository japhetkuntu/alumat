using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// Manage financial contributions to campaigns.
/// </summary>
[Authorize]
public class ContributionsController(IContributionService contributionService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of contributions with optional filters.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List contributions", Description = "Filter by campaignId and/or status.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ContributionDto>>))]
    public async Task<IActionResult> GetContributions([FromQuery] ContributionAdminFilter filter)
    {
        var admin = User.GetAccount();
        var result = await contributionService.GetContributionsAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Record a manual (offline) contribution.
    /// </summary>
    [HttpPost("manual")]
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
