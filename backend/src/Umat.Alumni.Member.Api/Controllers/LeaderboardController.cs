using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class LeaderboardController(ILeaderboardService leaderboardService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Year group leaderboard", Description = "Get the year group leaderboard sorted by membership rate and contributions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<YearGroupLeaderboardEntryDto>>))]
    public async Task<IActionResult> GetLeaderboard()
    {
        var result = await leaderboardService.GetLeaderboardAsync();
        return result.ToActionResult();
    }
}
