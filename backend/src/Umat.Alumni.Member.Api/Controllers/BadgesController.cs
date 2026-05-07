using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class BadgesController(IBadgeService badgeService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "My badges", Description = "Get all badges earned by the current member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<MemberBadgeDto>>))]
    public async Task<IActionResult> GetMyBadges()
    {
        var member = User.GetAccount();
        var result = await badgeService.GetMyBadgesAsync(member.Id);
        return result.ToActionResult();
    }

    [HttpPost("evaluate")]
    [SwaggerOperation(Summary = "Evaluate badges", Description = "Evaluate and award any new badges the member has earned")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<MemberBadgeDto>>))]
    public async Task<IActionResult> EvaluateBadges()
    {
        var member = User.GetAccount();
        await badgeService.EvaluateAndAwardBadgesAsync(member.Id);
        var result = await badgeService.GetMyBadgesAsync(member.Id);
        return result.ToActionResult();
    }
}
