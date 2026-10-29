using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Votes and elections the member is invited to. Which institutions have them is a platform setting.</summary>
[Authorize]
[RequireFeature(InstitutionFeatures.Polls)]
public class PollsController(IMemberPollService polls) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Polls open to me", Description = "Open first, then scheduled, then recently closed")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<MemberPollListItem>>))]
    public async Task<IActionResult> GetPolls() => (await polls.GetPollsAsync(User.GetAccount())).ToActionResult();

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "A poll's ballot, and whether I have voted")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberPollDetail>))]
    public async Task<IActionResult> GetPoll(string id) => (await polls.GetPollAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/vote")]
    [SwaggerOperation(Summary = "Cast my ballot", Description = "One answer for every position. Cannot be changed afterwards. What was chosen is not stored against the member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Vote(string id, [FromBody] CastBallotRequest request) => (await polls.CastBallotAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpGet("{id}/results")]
    [SwaggerOperation(Summary = "The results, when members may see them")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberPollResults>))]
    public async Task<IActionResult> Results(string id) => (await polls.GetResultsAsync(id, User.GetAccount())).ToActionResult();
}
