using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>Connect WhatsApp groups to the AlumUnion Assistant and post content to them. Full administrators only.</summary>
[Authorize(Roles = "SuperAdmin")]
[RequireFeature(InstitutionFeatures.WhatsApp)]
public class WhatsAppController(IWhatsAppGroupService groups) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "WhatsApp overview", Description = "Assistant number, platform switch, and this institution's connected groups")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppOverviewDto>))]
    public async Task<IActionResult> Overview() => (await groups.GetOverviewAsync()).ToActionResult();

    [HttpGet("adoption")]
    [SwaggerOperation(Summary = "WhatsApp adoption", Description = "How many members are connected, and how alerts were delivered by month. Counts only")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppAdoptionDto>))]
    public async Task<IActionResult> Adoption() => (await groups.GetAdoptionAsync()).ToActionResult();

    [HttpPost("groups")]
    [SwaggerOperation(Summary = "Start connecting a group", Description = "Creates a one-time code (24 hours) to post in the WhatsApp group as !link CODE")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<WhatsAppGroupDto>))]
    public async Task<IActionResult> Create([FromBody] CreateWhatsAppGroupRequest request)
        => (await groups.CreateAsync(request, User.GetAccount())).ToActionResult();

    [HttpPost("groups/{id}/confirm")]
    [SwaggerOperation(Summary = "Confirm a detected group", Description = "Makes a group that answered the code active")]
    public async Task<IActionResult> Confirm(string id) => (await groups.ConfirmAsync(id, User.GetAccount())).ToActionResult();

    [HttpPut("groups/{id}")]
    [SwaggerOperation(Summary = "Update a group's settings")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateWhatsAppGroupRequest request)
        => (await groups.UpdateAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpDelete("groups/{id}")]
    [SwaggerOperation(Summary = "Disconnect a group")]
    public async Task<IActionResult> Delete(string id) => (await groups.DeleteAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("publish/preview")]
    [SwaggerOperation(Summary = "Preview a post to a group", Description = "Builds the exact message and link without sending anything")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppPublishPreviewDto>))]
    public async Task<IActionResult> Preview([FromBody] WhatsAppPublishRequest request) => (await groups.PreviewAsync(request)).ToActionResult();

    [HttpPost("publish")]
    [SwaggerOperation(Summary = "Post to a group", Description = "Queues the confirmed message to a connected group")]
    public async Task<IActionResult> Publish([FromBody] WhatsAppPublishRequest request)
        => (await groups.PublishAsync(request, User.GetAccount())).ToActionResult();
}
