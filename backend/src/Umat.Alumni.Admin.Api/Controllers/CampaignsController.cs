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
/// Manage fundraising campaigns.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
public class CampaignsController(ICampaignService campaignService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of campaigns.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List campaigns")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<CampaignDto>>))]
    public async Task<IActionResult> GetCampaigns([FromQuery] CampaignFilter filter)
    {
        var admin = User.GetAccount();
        var result = await campaignService.GetCampaignsAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a campaign by ID.
    /// </summary>
    [HttpGet("{campaignId}")]
    [SwaggerOperation(Summary = "Get campaign by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetCampaign(string campaignId)
    {
        var admin = User.GetAccount();
        var result = await campaignService.GetCampaignByIdAsync(campaignId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new campaign.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create campaign")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateCampaign([FromForm] CreateCampaignRequest request)
    {
        var admin = User.GetAccount();
        var result = await campaignService.CreateCampaignAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Update an existing campaign.
    /// </summary>
    [HttpPut("{campaignId}")]
    [SwaggerOperation(Summary = "Update campaign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateCampaign(string campaignId, [FromForm] UpdateCampaignRequest request)
    {
        request.CampaignId = campaignId;
        var admin = User.GetAccount();
        var result = await campaignService.UpdateCampaignAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a campaign.
    /// </summary>
    [HttpDelete("{campaignId}")]
    [SwaggerOperation(Summary = "Delete campaign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteCampaign(string campaignId)
    {
        var admin = User.GetAccount();
        var result = await campaignService.DeleteCampaignAsync(campaignId, admin);
        return result.ToActionResult();
    }

    [HttpPut("{campaignId}/archive")]
    [SwaggerOperation(Summary = "Archive campaign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ArchiveCampaign(string campaignId)
    {
        var admin = User.GetAccount();
        var result = await campaignService.ArchiveCampaignAsync(campaignId, admin);
        return result.ToActionResult();
    }

    [HttpPut("{campaignId}/unarchive")]
    [SwaggerOperation(Summary = "Unarchive campaign (restores to Closed)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UnarchiveCampaign(string campaignId)
    {
        var admin = User.GetAccount();
        var result = await campaignService.UnarchiveCampaignAsync(campaignId, admin);
        return result.ToActionResult();
    }

    [HttpPut("{campaignId}/activate")]
    [SwaggerOperation(Summary = "Activate campaign")] 
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ActivateCampaign(string campaignId)
    {
        var admin = User.GetAccount();
        var result = await campaignService.ActivateCampaignAsync(campaignId, admin);
        return result.ToActionResult();
    }
    [HttpGet("{campaignId}/paystack-summary")]
    [SwaggerOperation(Summary = "Get campaign paystack contribution summary")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PaystackDisbursementSummaryDto>))]
    public async Task<IActionResult> GetCampaignPaystackSummary(string campaignId)
    {
        var admin = User.GetAccount();
        var result = await campaignService.GetCampaignPaystackSummaryAsync(campaignId, admin);
        return result.ToActionResult();
    }

    [HttpPut("{campaignId}/paystack-disburse")]
    [SwaggerOperation(Summary = "Mark campaign paystack contributions as disbursed")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkCampaignPaystackDisbursed(string campaignId)
    {
        var admin = User.GetAccount();
        var result = await campaignService.MarkCampaignPaystackDisbursedAsync(campaignId, admin);
        return result.ToActionResult();
    }}
