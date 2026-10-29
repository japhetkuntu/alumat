using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Elections: a ballot of positions with aspirants, an audience and open and close times. SuperAdmin only. Which institutions have
/// this at all is a platform setting (the Polls feature).
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[RequireFeature(InstitutionFeatures.Polls)]
public class PollsController(IPollService polls) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List polls", Description = "Active polls, or the archived ones with archived=true")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PollListItem>>))]
    public async Task<IActionResult> GetPolls([FromQuery] bool archived = false) => (await polls.GetPollsAsync(archived)).ToActionResult();

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get a poll with its ballot")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollDetail>))]
    public async Task<IActionResult> GetPoll(string id) => (await polls.GetPollAsync(id)).ToActionResult();

    [HttpPost]
    [SwaggerOperation(Summary = "Create a poll as a draft")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollDetail>))]
    public async Task<IActionResult> Create([FromBody] SavePollRequest request) => (await polls.CreatePollAsync(request, User.GetAccount())).ToActionResult();

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Update a poll", Description = "Once voting has started only the closing time, wording and results mode can change")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollDetail>))]
    public async Task<IActionResult> Update(string id, [FromBody] SavePollRequest request) => (await polls.UpdatePollAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/publish")]
    [SwaggerOperation(Summary = "Publish a draft to members")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollDetail>))]
    public async Task<IActionResult> Publish(string id) => (await polls.PublishAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/unpublish")]
    [SwaggerOperation(Summary = "Take a published poll back to a draft", Description = "Only while nobody has voted")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollDetail>))]
    public async Task<IActionResult> Unpublish(string id) => (await polls.UnpublishAsync(id, User.GetAccount())).ToActionResult();

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete a poll nobody has voted in")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Delete(string id) => (await polls.DeleteAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/archive")]
    [SwaggerOperation(Summary = "Archive a closed poll")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollDetail>))]
    public async Task<IActionResult> Archive(string id) => (await polls.SetArchivedAsync(id, true, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/restore")]
    [SwaggerOperation(Summary = "Bring an archived poll back to the list")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollDetail>))]
    public async Task<IActionResult> Restore(string id) => (await polls.SetArchivedAsync(id, false, User.GetAccount())).ToActionResult();

    [HttpGet("{id}/results")]
    [SwaggerOperation(Summary = "Turnout, and the figures once voting has closed or the poll runs live")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollResultsDto>))]
    public async Task<IActionResult> Results(string id) => (await polls.GetResultsAsync(id)).ToActionResult();

    [HttpPost("{id}/publish-results")]
    [SwaggerOperation(Summary = "Show the results to members", Description = "For a poll set to publish results by hand, once voting has closed")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PollResultsDto>))]
    public async Task<IActionResult> PublishResults(string id) => (await polls.PublishResultsAsync(id, User.GetAccount())).ToActionResult();
}
