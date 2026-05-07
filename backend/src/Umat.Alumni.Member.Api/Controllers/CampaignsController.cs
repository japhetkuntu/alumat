using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class CampaignsController(ICampaignService campaignService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List campaigns", Description = "Get a paginated list of active campaigns")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<CampaignDto>>))]
    public async Task<IActionResult> GetCampaigns([FromQuery] BaseFilter filter)
    {
        var member = User.GetAccount();
        var result = await campaignService.GetActiveCampaignsAsync(filter, member.Id);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("{campaignId}")]
    [SwaggerOperation(Summary = "Get campaign", Description = "Get details of a specific campaign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetCampaign(string campaignId)
    {
        var result = await campaignService.GetCampaignByIdAsync(campaignId);
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
