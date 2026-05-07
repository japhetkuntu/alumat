using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class SpotlightsController(ISpotlightService spotlightService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List approved spotlights", Description = "Get a paginated list of approved alumni spotlights")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<SpotlightDto>>))]
    public async Task<IActionResult> GetSpotlights([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await spotlightService.GetApprovedSpotlightsAsync(page, pageSize);
        return result.ToActionResult();
    }

    [HttpGet("{spotlightId}")]
    [SwaggerOperation(Summary = "Get spotlight by ID", Description = "Get full details of a single spotlight")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetSpotlight(string spotlightId)
    {
        var result = await spotlightService.GetSpotlightByIdAsync(spotlightId);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Submit spotlight", Description = "Submit a spotlight nomination for admin review")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<SpotlightDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SubmitSpotlight([FromBody] SubmitSpotlightRequest request)
    {
        var member = User.GetAccount();
        var result = await spotlightService.SubmitSpotlightAsync(request, member);
        return result.ToActionResult();
    }

    [HttpGet("mine")]
    [SwaggerOperation(Summary = "My spotlights", Description = "Get all spotlights submitted by the current member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<SpotlightDto>>))]
    public async Task<IActionResult> GetMySpotlights()
    {
        var member = User.GetAccount();
        var result = await spotlightService.GetMySpotlightsAsync(member.Id);
        return result.ToActionResult();
    }
}
