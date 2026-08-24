using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage this institution's graduating-class year groups ("batches") — lets
/// admins define their own list instead of the platform's generic year range.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v{version:apiVersion}/batches")]
public class BatchesController(IBatchService batchService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List batches")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<BatchListItem>>))]
    public async Task<IActionResult> GetBatches()
    {
        var result = await batchService.GetBatchesAsync();
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create a batch")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<BatchListItem>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest request)
    {
        var actor = User.GetAccount();
        var result = await batchService.CreateBatchAsync(request, actor.Id);
        return result.ToActionResult();
    }

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Update a batch")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BatchListItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateBatch(string id, [FromBody] UpdateBatchRequest request)
    {
        var actor = User.GetAccount();
        var result = await batchService.UpdateBatchAsync(id, request, actor.Id);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete a batch")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteBatch(string id)
    {
        var result = await batchService.DeleteBatchAsync(id);
        return result.ToActionResult();
    }
}
