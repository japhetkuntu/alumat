using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// Manage forum categories and threads.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
public class ForumController(IForumService forumService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of forum categories.
    /// </summary>
    [HttpGet("categories")]
    [SwaggerOperation(Summary = "List forum categories")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ForumCategoryDto>>))]
    public async Task<IActionResult> GetCategories([FromQuery] BaseFilter filter)
    {
        var result = await forumService.GetCategoriesAsync(filter);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new forum category.
    /// </summary>
    [HttpPost("categories")]
    [SwaggerOperation(Summary = "Create forum category")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<ForumCategoryDto>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var admin = User.GetAccount();
        var result = await forumService.CreateCategoryAsync(request.Name, request.Description, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a paginated list of forum threads.
    /// </summary>
    [HttpGet("threads")]
    [SwaggerOperation(Summary = "List forum threads", Description = "Optionally filter by categoryId.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ForumThreadDto>>))]
    public async Task<IActionResult> GetThreads([FromQuery] ForumThreadFilter filter)
    {
        var result = await forumService.GetThreadsAsync(filter);
        return result.ToActionResult();
    }

    /// <summary>
    /// Toggle pin status of a thread.
    /// </summary>
    [HttpPut("threads/{threadId}/pin")]
    [SwaggerOperation(Summary = "Pin/unpin thread")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> PinThread(string threadId)
    {
        var admin = User.GetAccount();
        var result = await forumService.PinThreadAsync(threadId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Toggle close status of a thread.
    /// </summary>
    [HttpPut("threads/{threadId}/close")]
    [SwaggerOperation(Summary = "Close/reopen thread")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CloseThread(string threadId)
    {
        var admin = User.GetAccount();
        var result = await forumService.CloseThreadAsync(threadId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a forum thread.
    /// </summary>
    [HttpDelete("threads/{threadId}")]
    [SwaggerOperation(Summary = "Delete thread")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteThread(string threadId)
    {
        var admin = User.GetAccount();
        var result = await forumService.DeleteThreadAsync(threadId, admin);
        return result.ToActionResult();
    }
}
