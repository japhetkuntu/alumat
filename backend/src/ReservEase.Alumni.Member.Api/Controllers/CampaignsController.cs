using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
[RequireFeature(InstitutionFeatures.Contributions)]
public class CampaignsController(ICampaignService campaignService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List campaigns", Description = "Get a paginated list of active campaigns")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<CampaignDto>>))]
    public async Task<IActionResult> GetCampaigns([FromQuery] BaseFilter filter, [FromQuery] string? communityId = null)
    {
        var member = User.GetAccount();
        var result = await campaignService.GetActiveCampaignsAsync(filter, member.Id, communityId);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("{campaignId}")]
    [SwaggerOperation(Summary = "Get campaign", Description = "Get details of a specific campaign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetCampaign(string campaignId)
    {
        // AllowAnonymous — GetAccount() returns an empty id when there's no
        // token, which the service treats as "no member context" and uses to
        // hide any community-scoped campaign from anonymous/non-member callers.
        var member = User.GetAccount();
        var result = await campaignService.GetCampaignByIdAsync(campaignId, string.IsNullOrEmpty(member.Id) ? null : member.Id);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("membership/current")]
    [SwaggerOperation(Summary = "Get current membership campaign", Description = "Get the active membership campaign for the current year — no authentication required")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    public async Task<IActionResult> GetCurrentMembershipCampaign()
    {
        var result = await campaignService.GetCurrentMembershipCampaignAsync();
        return result.ToActionResult();
    }
}
