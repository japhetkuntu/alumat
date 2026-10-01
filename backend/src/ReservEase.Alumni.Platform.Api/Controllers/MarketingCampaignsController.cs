using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

[Authorize(Roles = "SuperAdmin,Sales")]
[Route("api/v{version:apiVersion}/marketing-campaigns")]
public class MarketingCampaignsController(IMarketingCampaignService marketing) : DefaultController
{
    public record CampaignRequest([Required, MaxLength(200)] string Title, [MaxLength(4000)] string Description, string Status);
    public record PostRequest([Required, MaxLength(200)] string Title, [MaxLength(10000)] string Caption, [MaxLength(20000)] string Content, string DestinationPath, bool UseLandingPage, string Status, Dictionary<string, string> Captions, List<string> AssetIds);
    public record ShareRequest(string Channel);
    public record SharedRequest(string? PublishedUrl);
    public record LinkStateRequest(bool Disabled);

    private string Actor => User.GetAccount().Id;

    /// <summary>Every service method returns (StatusCode, Body) — see IMarketingCampaignService for why
    /// this feature doesn't use the codebase's usual IApiResponse envelope. This just forwards it.</summary>
    private IActionResult Forward((int Status, object? Body) result) => StatusCode(result.Status, result.Body);

    [HttpGet]
    public async Task<IActionResult> List() => Forward(await marketing.ListCampaignsAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CampaignRequest r) => Forward(await marketing.CreateCampaignAsync(new(r.Title, r.Description, r.Status), Actor));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, CampaignRequest r) => Forward(await marketing.UpdateCampaignAsync(id, new(r.Title, r.Description, r.Status), Actor));

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id) => Forward(await marketing.GetCampaignDetailAsync(id));

    [HttpPut("{id}/posts/{postId}")]
    public async Task<IActionResult> SavePost(string id, string postId, PostRequest r) =>
        Forward(await marketing.SavePostAsync(id, postId, new(r.Title, r.Caption, r.Content, r.DestinationPath, r.UseLandingPage, r.Status, r.Captions, r.AssetIds), Actor));

    [HttpPost("{id}/assets")]
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    public async Task<IActionResult> Upload(string id, IFormFile file) => Forward(await marketing.UploadAssetAsync(id, file, Actor));

    [HttpDelete("{id}/assets/{assetId}")]
    public async Task<IActionResult> DeleteAsset(string id, string assetId) => Forward(await marketing.DeleteAssetAsync(id, assetId, Actor));

    [HttpDelete("{id}/posts/{postId}")]
    public async Task<IActionResult> DeletePost(string id, string postId) => Forward(await marketing.DeletePostAsync(id, postId, Actor));

    [HttpPost("{id}/posts/{postId}/shares")]
    public async Task<IActionResult> Prepare(string id, string postId, ShareRequest r) => Forward(await marketing.PrepareShareAsync(id, postId, r.Channel, Actor));

    [HttpPost("{id}/shares/{shareId}/confirm")]
    public async Task<IActionResult> Confirm(string id, string shareId, SharedRequest r) => Forward(await marketing.ConfirmShareAsync(id, shareId, r.PublishedUrl, Actor));

    [HttpPut("{id}/shares/{shareId}/link")]
    public async Task<IActionResult> SetLinkState(string id, string shareId, LinkStateRequest r) => Forward(await marketing.SetShareLinkStateAsync(id, shareId, r.Disabled, Actor));

    [HttpDelete("{id}/shares/{shareId}")]
    public async Task<IActionResult> DeleteShare(string id, string shareId) => Forward(await marketing.DeleteShareAsync(id, shareId, Actor));
}
