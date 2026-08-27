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
[RequireFeature(InstitutionFeatures.News)]
public class NewsController(IMemberNewsService newsService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List news", Description = "Get a paginated list of news posts, optionally filtered by category")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<NewsPostDto>>))]
    public async Task<IActionResult> GetPosts([FromQuery] NewsFilter filter)
    {
        var member = User.GetAccount();
        var result = await newsService.GetPostsAsync(filter, member.Id);
        return result.ToActionResult();
    }

    [HttpGet("{postId}")]
    [SwaggerOperation(Summary = "Get news post", Description = "Get details of a specific news post")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<NewsPostDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetPost(string postId)
    {
        var member = User.GetAccount();
        var result = await newsService.GetPostByIdAsync(postId, member.Id);
        return result.ToActionResult();
    }
}
