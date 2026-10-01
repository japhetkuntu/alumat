namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

/// <summary>
/// Prepare-once, share-when-needed marketing content for platform staff — campaign → posts →
/// per-channel tracked shares. Deliberately separate from institutions' own fundraising
/// campaigns (MarketingCampaign vs. Campaign). See MarketingCampaignsController for the HTTP
/// surface and MarketingRules for the shared validation/whitelist logic.
///
/// Every method returns (StatusCode, Body) rather than the codebase's usual IApiResponse — this
/// feature's frontend (marketing-api.ts) already expects a bare JSON body or bare error string,
/// not the ApiResponse envelope, and changing that would mean also touching already-working
/// frontend code for no functional benefit. The controller just forwards whatever's returned.
/// </summary>
public interface IMarketingCampaignService
{
    Task<(int Status, object? Body)> ListCampaignsAsync();
    Task<(int Status, object? Body)> GetCampaignDetailAsync(string campaignId);
    Task<(int Status, object? Body)> CreateCampaignAsync(CampaignInput input, string actorId);
    Task<(int Status, object? Body)> UpdateCampaignAsync(string campaignId, CampaignInput input, string actorId);
    Task<(int Status, object? Body)> SavePostAsync(string campaignId, string postId, PostInput input, string actorId);
    Task<(int Status, object? Body)> UploadAssetAsync(string campaignId, Microsoft.AspNetCore.Http.IFormFile file, string actorId);
    Task<(int Status, object? Body)> DeleteAssetAsync(string campaignId, string assetId, string actorId);
    Task<(int Status, object? Body)> DeletePostAsync(string campaignId, string postId, string actorId);
    Task<(int Status, object? Body)> PrepareShareAsync(string campaignId, string postId, string channel, string actorId);
    Task<(int Status, object? Body)> ConfirmShareAsync(string campaignId, string shareId, string? publishedUrl, string actorId);
    Task<(int Status, object? Body)> SetShareLinkStateAsync(string campaignId, string shareId, bool disabled, string actorId);
    Task<(int Status, object? Body)> DeleteShareAsync(string campaignId, string shareId, string actorId);
}

public record CampaignInput(string Title, string Description, string Status);
public record PostInput(string Title, string Caption, string Content, string DestinationPath, bool UseLandingPage, string Status, Dictionary<string, string>? Captions, List<string>? AssetIds);
