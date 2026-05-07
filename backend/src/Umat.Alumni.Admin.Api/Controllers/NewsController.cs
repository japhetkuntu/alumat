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
/// Manage news posts.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
public class NewsController(INewsService newsService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of news posts.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List news posts", Description = "Filter by status (Draft, Published).")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<NewsPostDto>>))]
    public async Task<IActionResult> GetPosts([FromQuery] NewsFilter filter)
    {
        var admin = User.GetAccount();
        var result = await newsService.GetPostsAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a single news post by ID.
    /// </summary>
    [HttpGet("{postId}")]
    [SwaggerOperation(Summary = "Get news post by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<NewsPostDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetPost(string postId)
    {
        var admin = User.GetAccount();
        var result = await newsService.GetPostAsync(postId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new news post.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create news post")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<NewsPostDto>))]
    public async Task<IActionResult> CreatePost([FromForm] CreateNewsPostRequest request)
    {
        var admin = User.GetAccount();
        var result = await newsService.CreatePostAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Update an existing news post.
    /// </summary>
    [HttpPut("{postId}")]
    [SwaggerOperation(Summary = "Update news post")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<NewsPostDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdatePost(string postId, [FromForm] UpdateNewsPostRequest request)
    {
        var admin = User.GetAccount();
        request.PostId = postId;
        var result = await newsService.UpdatePostAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Publish a draft news post.
    /// </summary>
    [HttpPut("{postId}/publish")]
    [SwaggerOperation(Summary = "Publish news post")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<NewsPostDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> PublishPost(string postId)
    {
        var admin = User.GetAccount();
        var result = await newsService.PublishPostAsync(postId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a news post.
    /// </summary>
    [HttpDelete("{postId}")]
    [SwaggerOperation(Summary = "Delete news post")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeletePost(string postId)
    {
        var admin = User.GetAccount();
        var result = await newsService.DeletePostAsync(postId, admin);
        return result.ToActionResult();
    }
}
