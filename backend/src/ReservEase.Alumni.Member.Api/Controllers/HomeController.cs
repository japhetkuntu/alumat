using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// Not behind a single [RequireFeature]: home spans every feature, so the institution's
/// disabled list goes to the service, which leaves switched-off modules out.
/// </summary>
[Authorize]
public class HomeController(IHomeService homeService) : DefaultController
{
    private IReadOnlyCollection<string> DisabledFeatures =>
        HttpContext.Items["Institution"] is Institution institution ? institution.DisabledFeatures : [];

    [HttpGet("feed")]
    [SwaggerOperation(Summary = "Home feed", Description = "Recent activity by named members, with the time the member last opened home so the portal can mark what is new")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<HomeFeedDto>))]
    public async Task<IActionResult> GetFeed()
    {
        var result = await homeService.GetFeedAsync(User.GetAccount().Id, DisabledFeatures);
        return result.ToActionResult();
    }

    [HttpGet("modules")]
    [SwaggerOperation(Summary = "Module activity", Description = "Which enabled features have nothing in them yet for this member, so the portal can leave them out of its navigation")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<HomeModulesDto>))]
    public async Task<IActionResult> GetModules()
    {
        var result = await homeService.GetModulesAsync(User.GetAccount().Id, DisabledFeatures);
        return result.ToActionResult();
    }

    [HttpGet("next-steps")]
    [SwaggerOperation(Summary = "Next steps", Description = "Up to three things worth doing next, chosen by fixed rules from what exists for this member. Never a request for money.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<NextStepDto>>))]
    public async Task<IActionResult> GetNextSteps()
    {
        var result = await homeService.GetNextStepsAsync(User.GetAccount().Id, DisabledFeatures);
        return result.ToActionResult();
    }

    [HttpPost("seen")]
    [SwaggerOperation(Summary = "Mark home seen", Description = "Record that the member has seen their home feed up to now")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkSeen()
    {
        var result = await homeService.MarkSeenAsync(User.GetAccount().Id);
        return result.ToActionResult();
    }
}
