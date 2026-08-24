using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
[RequireFeature(InstitutionFeatures.Leaderboard)]
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
