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

/// <summary>Members asking each other for an introduction, and the private chat once it is accepted.</summary>
[Authorize]
[RequireFeature(InstitutionFeatures.Introductions)]
public class IntroductionsController(IMemberIntroductionService introductions) : DefaultController
{
    [HttpGet("with/{memberId}")]
    [SwaggerOperation(Summary = "Where I stand with one member", Description = "none, pending, incoming, connected, unavailable or self.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntroductionStateDto>))]
    public async Task<IActionResult> GetState(string memberId) => (await introductions.GetStateAsync(memberId, User.GetAccount())).ToActionResult();

    [HttpPost]
    [SwaggerOperation(Summary = "Ask a member for an introduction", Description = "A short note saying why. Nothing is shared and no message can be sent until they accept.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntroductionStateDto>))]
    public async Task<IActionResult> AskForIntroduction([FromBody] RequestIntroductionRequest request) => (await introductions.RequestAsync(request, User.GetAccount())).ToActionResult();

    [HttpGet]
    [SwaggerOperation(Summary = "My conversations and requests", Description = "box = chats, incoming or sent.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntroductionPageDto>))]
    public async Task<IActionResult> List([FromQuery] string box = "chats", [FromQuery] string? cursor = null, [FromQuery] int limit = 25, [FromQuery] string? q = null)
        => (await introductions.ListAsync(box, cursor, limit, q, User.GetAccount())).ToActionResult();

    [HttpGet("summary")]
    [SwaggerOperation(Summary = "Unread chats and waiting requests, for the menu badge")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntroductionSummaryDto>))]
    public async Task<IActionResult> Summary() => (await introductions.GetSummaryAsync(User.GetAccount())).ToActionResult();

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Open a conversation", Description = "Returns the messages, or only those after `after` when polling. Opening it marks it read.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntroductionDetailDto>))]
    public async Task<IActionResult> Get(string id, [FromQuery] DateTime? after = null, [FromQuery] DateTime? before = null, [FromQuery] int? limit = null)
        => (await introductions.GetAsync(id, after, User.GetAccount(), before, limit)).ToActionResult();

    [HttpPost("{id}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Accept(string id) => (await introductions.RespondAsync(id, true, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Decline(string id) => (await introductions.RespondAsync(id, false, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/withdraw")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Withdraw(string id) => (await introductions.WithdrawAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/end")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> End(string id) => (await introductions.EndAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/resume")]
    [SwaggerOperation(Summary = "Resume a conversation I ended")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Resume(string id) => (await introductions.ResumeAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/messages")]
    [SwaggerOperation(Summary = "Send a message", Description = "Only in an accepted conversation.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntroductionMessageDto>))]
    public async Task<IActionResult> Send(string id, [FromBody] SendIntroductionMessageRequest request) => (await introductions.SendMessageAsync(id, request.Body, User.GetAccount())).ToActionResult();

    [HttpPost("{id}/report")]
    [SwaggerOperation(Summary = "Report a conversation", Description = "Sends the reporter's chosen reason and the latest messages to the institution's admins. Admins never see a conversation otherwise.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Report(string id, [FromBody] ReportIntroductionRequest request) => (await introductions.ReportAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpGet("blocks")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<IntroductionPersonDto>>))]
    public async Task<IActionResult> Blocked() => (await introductions.ListBlockedAsync(User.GetAccount())).ToActionResult();

    [HttpPost("blocks")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Block([FromBody] BlockMemberRequest request) => (await introductions.BlockAsync(request.MemberId, User.GetAccount())).ToActionResult();

    [HttpDelete("blocks/{memberId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Unblock(string memberId) => (await introductions.UnblockAsync(memberId, User.GetAccount())).ToActionResult();
}
