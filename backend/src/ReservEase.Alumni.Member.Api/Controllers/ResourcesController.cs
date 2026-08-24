using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
[RequireFeature(InstitutionFeatures.Resources)]
public class ResourcesController(IMemberResourceService resourceService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List resources", Description = "Get a paginated list of alumni resources, optionally filtered by category")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ResourceDto>>))]
    public async Task<IActionResult> GetResources([FromQuery] ResourceFilter filter)
    {
        var member = User.GetAccount();
        var result = await resourceService.GetResourcesAsync(filter, member.Id);
        return result.ToActionResult();
    }

    [HttpGet("{resourceId}")]
    [SwaggerOperation(Summary = "Get resource by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ResourceDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetResource(string resourceId)
    {
        var result = await resourceService.GetResourceAsync(resourceId);
        return result.ToActionResult();
    }

    [HttpPost("{resourceId}/download")]
    [SwaggerOperation(Summary = "Track resource download")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> TrackResourceDownload(string resourceId)
    {
        var result = await resourceService.IncrementDownloadCountAsync(resourceId);
        return result.ToActionResult();
    }
}
