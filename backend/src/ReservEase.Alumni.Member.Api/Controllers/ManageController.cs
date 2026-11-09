using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>What a member does as a leader of a year group or community, within the privileges of the roles they hold there.</summary>
[Authorize]
public class ManageController(IManageService manageService, IManageContentService contentService, IManageFundraisingService fundraisingService, IManageCountsService countsService) : DefaultController
{
    [HttpGet("groups/{scopeType}/{scopeId}/counts")]
    [SwaggerOperation(Summary = "What each section of a group's management area holds")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ManageCountsDto>))]
    public async Task<IActionResult> GetCounts(string scopeType, string scopeId)
        => (await countsService.GetAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpGet("groups")]
    [SwaggerOperation(Summary = "The groups I can manage, and what I can do in each")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<ManageGroupDto>>))]
    public async Task<IActionResult> GetGroups() => (await manageService.GetGroupsAsync(User.GetAccount().Id)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/summary")]
    [SwaggerOperation(Summary = "A group's report")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<GroupSummaryDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetSummary(string scopeType, string scopeId)
        => (await manageService.GetSummaryAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/members")]
    [SwaggerOperation(Summary = "A group's members and waiting requests")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupMemberDto>>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetMembers(string scopeType, string scopeId)
        => (await manageService.GetMembersAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/members/{memberId}/decision")]
    [SwaggerOperation(Summary = "Approve or decline a request to join")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Decide(string scopeType, string scopeId, string memberId, [FromBody] DecideRequest request)
        => (await manageService.DecideAsync(User.GetAccount().Id, scopeType, scopeId, memberId, request.Approve)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/message")]
    [SwaggerOperation(Summary = "Message everyone in a group")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<GroupMessageResult>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Message(string scopeType, string scopeId, [FromBody] GroupMessageRequest request)
        => (await manageService.MessageAsync(User.GetAccount().Id, scopeType, scopeId, request.Text, request.Channels)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/messages")]
    [SwaggerOperation(Summary = "What has already been sent to a group")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupMessageHistoryDto>>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetMessages(string scopeType, string scopeId)
        => (await manageService.GetMessagesAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/channels")]
    [SwaggerOperation(Summary = "Which ways of reaching the group are open to this community")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ManageChannelsDto>))]
    public async Task<IActionResult> GetChannels(string scopeType, string scopeId)
        => (await manageService.GetChannelsAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/events")]
    [SwaggerOperation(Summary = "A group's events")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupEventDto>>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetEvents(string scopeType, string scopeId)
        => (await manageService.GetEventsAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/events")]
    [SwaggerOperation(Summary = "Create a free event for a group")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<GroupEventDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateEvent(string scopeType, string scopeId, [FromForm] SaveGroupEventRequest request)
        => (await manageService.CreateEventAsync(User.GetAccount().Id, scopeType, scopeId, request)).ToActionResult();

    [HttpPut("groups/{scopeType}/{scopeId}/events/{eventId}")]
    [SwaggerOperation(Summary = "Edit an event you made")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<GroupEventDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateEvent(string scopeType, string scopeId, string eventId, [FromForm] SaveGroupEventRequest request)
        => (await manageService.UpdateEventAsync(User.GetAccount().Id, scopeType, scopeId, eventId, request)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/events/{eventId}/cancel")]
    [SwaggerOperation(Summary = "Cancel an event you made")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CancelEvent(string scopeType, string scopeId, string eventId)
        => (await manageService.CancelEventAsync(User.GetAccount().Id, scopeType, scopeId, eventId)).ToActionResult();

    // ── News, jobs and shared links ──────────────────────────────────────────

    [HttpGet("groups/{scopeType}/{scopeId}/news")]
    [SwaggerOperation(Summary = "A group's news")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupNewsDto>>))]
    public async Task<IActionResult> GetNews(string scopeType, string scopeId) => (await contentService.GetNewsAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/news")]
    [SwaggerOperation(Summary = "Post news to a group")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<GroupNewsDto>))]
    public async Task<IActionResult> CreateNews(string scopeType, string scopeId, [FromForm] SaveGroupNewsRequest request)
        => (await contentService.CreateNewsAsync(User.GetAccount().Id, scopeType, scopeId, request)).ToActionResult();

    [HttpPut("groups/{scopeType}/{scopeId}/news/{postId}")]
    [SwaggerOperation(Summary = "Edit news you posted")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<GroupNewsDto>))]
    public async Task<IActionResult> UpdateNews(string scopeType, string scopeId, string postId, [FromForm] SaveGroupNewsRequest request)
        => (await contentService.UpdateNewsAsync(User.GetAccount().Id, scopeType, scopeId, postId, request)).ToActionResult();

    [HttpDelete("groups/{scopeType}/{scopeId}/news/{postId}")]
    [SwaggerOperation(Summary = "Delete news you posted")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteNews(string scopeType, string scopeId, string postId)
        => (await contentService.DeleteNewsAsync(User.GetAccount().Id, scopeType, scopeId, postId)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/jobs")]
    [SwaggerOperation(Summary = "A group's jobs and opportunities")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupJobDto>>))]
    public async Task<IActionResult> GetJobs(string scopeType, string scopeId) => (await contentService.GetJobsAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/jobs")]
    [SwaggerOperation(Summary = "Share a job with a group")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<GroupJobDto>))]
    public async Task<IActionResult> CreateJob(string scopeType, string scopeId, [FromForm] SaveGroupJobRequest request)
        => (await contentService.CreateJobAsync(User.GetAccount().Id, scopeType, scopeId, request)).ToActionResult();

    [HttpPut("groups/{scopeType}/{scopeId}/jobs/{jobId}")]
    [SwaggerOperation(Summary = "Edit a job you shared")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<GroupJobDto>))]
    public async Task<IActionResult> UpdateJob(string scopeType, string scopeId, string jobId, [FromForm] SaveGroupJobRequest request)
        => (await contentService.UpdateJobAsync(User.GetAccount().Id, scopeType, scopeId, jobId, request)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/jobs/{jobId}/close")]
    [SwaggerOperation(Summary = "Close a job you shared")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CloseJob(string scopeType, string scopeId, string jobId)
        => (await contentService.CloseJobAsync(User.GetAccount().Id, scopeType, scopeId, jobId)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/resources")]
    [SwaggerOperation(Summary = "A group's shared links")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupResourceDto>>))]
    public async Task<IActionResult> GetResources(string scopeType, string scopeId) => (await contentService.GetResourcesAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/resources")]
    [SwaggerOperation(Summary = "Share a link with a group")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<GroupResourceDto>))]
    public async Task<IActionResult> CreateResource(string scopeType, string scopeId, [FromForm] SaveGroupResourceRequest request)
        => (await contentService.CreateResourceAsync(User.GetAccount().Id, scopeType, scopeId, request)).ToActionResult();

    [HttpPut("groups/{scopeType}/{scopeId}/resources/{resourceId}")]
    [SwaggerOperation(Summary = "Edit a link you shared")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<GroupResourceDto>))]
    public async Task<IActionResult> UpdateResource(string scopeType, string scopeId, string resourceId, [FromForm] SaveGroupResourceRequest request)
        => (await contentService.UpdateResourceAsync(User.GetAccount().Id, scopeType, scopeId, resourceId, request)).ToActionResult();

    [HttpDelete("groups/{scopeType}/{scopeId}/resources/{resourceId}")]
    [SwaggerOperation(Summary = "Remove a link you shared")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteResource(string scopeType, string scopeId, string resourceId)
        => (await contentService.DeleteResourceAsync(User.GetAccount().Id, scopeType, scopeId, resourceId)).ToActionResult();

    // ── Fundraisers ──────────────────────────────────────────────────────────

    [HttpGet("groups/{scopeType}/{scopeId}/campaigns")]
    [SwaggerOperation(Summary = "A group's fundraisers")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupCampaignDto>>))]
    public async Task<IActionResult> GetCampaigns(string scopeType, string scopeId) => (await fundraisingService.GetCampaignsAsync(User.GetAccount().Id, scopeType, scopeId)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/campaigns")]
    [SwaggerOperation(Summary = "Start a fundraiser for a group")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<GroupCampaignDto>))]
    public async Task<IActionResult> CreateCampaign(string scopeType, string scopeId, [FromForm] SaveGroupCampaignRequest request)
        => (await fundraisingService.CreateCampaignAsync(User.GetAccount().Id, scopeType, scopeId, request)).ToActionResult();

    [HttpPut("groups/{scopeType}/{scopeId}/campaigns/{campaignId}")]
    [SwaggerOperation(Summary = "Edit a fundraiser you started")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<GroupCampaignDto>))]
    public async Task<IActionResult> UpdateCampaign(string scopeType, string scopeId, string campaignId, [FromForm] SaveGroupCampaignRequest request)
        => (await fundraisingService.UpdateCampaignAsync(User.GetAccount().Id, scopeType, scopeId, campaignId, request)).ToActionResult();

    [HttpPost("groups/{scopeType}/{scopeId}/campaigns/{campaignId}/close")]
    [SwaggerOperation(Summary = "Close a fundraiser you started")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CloseCampaign(string scopeType, string scopeId, string campaignId)
        => (await fundraisingService.CloseCampaignAsync(User.GetAccount().Id, scopeType, scopeId, campaignId)).ToActionResult();

    [HttpGet("groups/{scopeType}/{scopeId}/campaigns/{campaignId}/gifts")]
    [SwaggerOperation(Summary = "Who has given to a fundraiser of the group")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<GroupGiftDto>>))]
    public async Task<IActionResult> GetGifts(string scopeType, string scopeId, string campaignId)
        => (await fundraisingService.GetGiftsAsync(User.GetAccount().Id, scopeType, scopeId, campaignId)).ToActionResult();
}
