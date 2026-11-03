using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// The institution's own website: edit a draft, then publish it. SuperAdmin only, because a published page speaks for the whole institution.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
public class WebsiteController(IWebsiteService websiteService, IWebsiteContentService contentService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Open the website editor", Description = "The draft (null until first opened), where it stands, and the institution's own branding.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WebsiteEditorDto>))]
    public async Task<IActionResult> Get() => (await websiteService.GetAsync()).ToActionResult();

    [HttpGet("content")]
    [SwaggerOperation(Summary = "The institution's own items as the website would show them", Description = "Same rules as the public site: only what is public, only features that are on.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WebsiteContentDto>))]
    public async Task<IActionResult> Content() => (await contentService.GetAsync()).ToActionResult();

    [HttpPut("draft")]
    [SwaggerOperation(Summary = "Save the draft", Description = "Autosave. BaseRevision must match the stored revision, or the save is refused (409) so two admins never overwrite each other.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WebsiteStatusDto>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SaveDraft([FromBody] SaveWebsiteDraftRequest request)
        => (await websiteService.SaveDraftAsync(request, User.GetAccount())).ToActionResult();

    [HttpPost("publish")]
    [SwaggerOperation(Summary = "Make the draft the live website")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WebsiteStatusDto>))]
    public async Task<IActionResult> Publish([FromBody] PublishWebsiteRequest request)
        => (await websiteService.PublishAsync(request, User.GetAccount())).ToActionResult();

    [HttpPost("unpublish")]
    [SwaggerOperation(Summary = "Go back to the standard landing page", Description = "Keeps the draft and every earlier version.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WebsiteStatusDto>))]
    public async Task<IActionResult> Unpublish() => (await websiteService.UnpublishAsync(User.GetAccount())).ToActionResult();

    [HttpGet("versions")]
    [SwaggerOperation(Summary = "Earlier published versions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<WebsiteVersionDto>>))]
    public async Task<IActionResult> Versions() => (await websiteService.GetVersionsAsync()).ToActionResult();

    [HttpPost("versions/{versionId}/restore")]
    [SwaggerOperation(Summary = "Put an earlier version back into the draft", Description = "Visitors see nothing change until it is published.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WebsiteStatusDto>))]
    public async Task<IActionResult> Restore(string versionId) => (await websiteService.RestoreVersionAsync(versionId, User.GetAccount())).ToActionResult();
}
