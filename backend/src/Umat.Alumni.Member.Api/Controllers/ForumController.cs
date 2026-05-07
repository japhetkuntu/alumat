using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class ForumController(IMemberForumService forumService) : DefaultController
{
    [HttpGet("categories")]
    [SwaggerOperation(Summary = "List categories", Description = "Get all forum categories")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ForumCategoryDto>>))]
    public async Task<IActionResult> GetCategories()
    {
        var result = await forumService.GetCategoriesAsync();
        return result.ToActionResult();
    }

    [HttpGet("threads")]
    [SwaggerOperation(Summary = "List threads", Description = "Get a paginated list of forum threads, optionally filtered by category")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ForumThreadDto>>))]
    public async Task<IActionResult> GetThreads([FromQuery] ForumThreadFilter filter)
    {
        var result = await forumService.GetThreadsAsync(filter);
        return result.ToActionResult();
    }

    [HttpPost("threads")]
    [SwaggerOperation(Summary = "Create thread", Description = "Create a new forum discussion thread")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ForumThreadDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request)
    {
        var member = User.GetAccount();
        var result = await forumService.CreateThreadAsync(request, member);
        return result.ToActionResult();
    }

    [HttpGet("threads/{threadId}/posts")]
    [SwaggerOperation(Summary = "List thread posts", Description = "Get a paginated list of posts in a specific forum thread")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ForumPostDto>>))]
    public async Task<IActionResult> GetPosts(string threadId, [FromQuery] BaseFilter filter)
    {
        var result = await forumService.GetPostsAsync(threadId, filter);
        return result.ToActionResult();
    }

    [HttpPost("threads/{threadId}/reply")]
    [SwaggerOperation(Summary = "Reply to thread", Description = "Post a reply to an existing forum thread")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ForumPostDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Reply(string threadId, [FromBody] ReplyBody body)
    {
        var member = User.GetAccount();
        var result = await forumService.ReplyToThreadAsync(
            new CreateForumPostRequest(threadId, body.Content), member);
        return result.ToActionResult();
    }
}

public record ReplyBody(string Content);
