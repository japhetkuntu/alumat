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
/// Manage alumni resources (files, links).
/// </summary>
[Authorize]
public class ResourcesController(IResourceService resourceService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of resources.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List resources", Description = "Optionally filter by category.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ResourceDto>>))]
    public async Task<IActionResult> GetResources([FromQuery] ResourceFilter filter)
    {
        var admin = User.GetAccount();
        var result = await resourceService.GetResourcesAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a single resource by ID.
    /// </summary>
    [HttpGet("{resourceId}")]
    [SwaggerOperation(Summary = "Get resource by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ResourceDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetResource(string resourceId)
    {
        var admin = User.GetAccount();
        var result = await resourceService.GetResourceAsync(resourceId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new resource.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create resource")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<ResourceDto>))]
    public async Task<IActionResult> CreateResource([FromForm] CreateResourceRequest request)
    {
        var admin = User.GetAccount();
        var result = await resourceService.CreateResourceAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a resource.
    /// </summary>
    [HttpDelete("{resourceId}")]
    [SwaggerOperation(Summary = "Delete resource")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteResource(string resourceId)
    {
        var admin = User.GetAccount();
        var result = await resourceService.DeleteResourceAsync(resourceId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Update an existing resource.
    /// </summary>
    [HttpPut("{resourceId}")]
    [SwaggerOperation(Summary = "Update resource")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ResourceDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateResource(string resourceId, [FromForm] UpdateResourceRequest request)
    {
        request.ResourceId = resourceId;
        var admin = User.GetAccount();
        var result = await resourceService.UpdateResourceAsync(request, admin);
        return result.ToActionResult();
    }
}
