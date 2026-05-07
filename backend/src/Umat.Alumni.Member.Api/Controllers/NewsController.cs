using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class NewsController(IMemberNewsService newsService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List news", Description = "Get a paginated list of news posts, optionally filtered by category")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<NewsPostDto>>))]
    public async Task<IActionResult> GetPosts([FromQuery] NewsFilter filter)
    {
        var result = await newsService.GetPostsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("{postId}")]
    [SwaggerOperation(Summary = "Get news post", Description = "Get details of a specific news post")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<NewsPostDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetPost(string postId)
    {
        var result = await newsService.GetPostByIdAsync(postId);
        return result.ToActionResult();
    }
}
