using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Marketing;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class MarketingCampaignService(
    IAlumniPgRepository<MarketingCampaign> campaignRepo,
    IAlumniPgRepository<MarketingPost> postRepo,
    IAlumniPgRepository<MarketingAsset> assetRepo,
    IAlumniPgRepository<MarketingShare> shareRepo,
    IAlumniPgRepository<MarketingVisit> visitRepo,
    IAlumniPgRepository<OnboardingLead> leadRepo,
    IStorageService storage,
    ILogger<MarketingCampaignService> logger) : IMarketingCampaignService
{
    private static object PostDto(MarketingPost p) => new
    {
        p.Id, p.CampaignId, p.Title, p.Caption, p.Content, p.DestinationPath, p.UseLandingPage, p.Status,
        Captions = JsonSerializer.Deserialize<Dictionary<string, string>>(p.CaptionsJson),
        AssetIds = JsonSerializer.Deserialize<List<string>>(p.AssetIdsJson),
    };

    public async Task<(int, object?)> ListCampaignsAsync()
    {
        var campaigns = campaignRepo.GetQueryable();
        var posts = postRepo.GetQueryable();
        var shares = shareRepo.GetQueryable();
        var leads = leadRepo.GetQueryable(ignoreQueryFilters: true);

        var list = await campaigns.OrderByDescending(c => c.CreatedAt).Select(c => new
        {
            c.Id,
            c.Title,
            c.Description,
            c.Status,
            c.CreatedAt,
            PostCount = posts.Count(p => p.CampaignId == c.Id),
            ShareCount = shares.Count(s => s.CampaignId == c.Id && s.SharedAt != null),
            Enquiries = leads.Count(l => shares.Any(s => s.CampaignId == c.Id && s.Id == l.MarketingShareId)),
        }).ToListAsync();
        return (200, list);
    }

    public async Task<(int, object?)> GetCampaignDetailAsync(string campaignId)
    {
        var campaign = await campaignRepo.GetByIdAsync(campaignId);
        if (campaign is null) return (404, null);

        var posts = await postRepo.GetAllAsync(p => p.CampaignId == campaignId);
        var assets = await assetRepo.GetAllAsync(a => a.CampaignId == campaignId);
        var visits = visitRepo.GetQueryable();
        var leads = leadRepo.GetQueryable(ignoreQueryFilters: true);

        var shares = await shareRepo.GetQueryable(s => s.CampaignId == campaignId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id, s.ShortCode, s.PostId, s.Channel, s.Caption, s.SnapshotJson, s.CreatedAt, s.CreatedBy, s.SharedAt, s.PublishedUrl, s.Disabled,
                Visits = visits.Count(v => v.ShareId == s.Id),
                Enquiries = leads.Count(l => l.MarketingShareId == s.Id),
            })
            .ToListAsync();

        return (200, new
        {
            campaign,
            posts = posts.OrderBy(p => p.CreatedAt).Select(PostDto),
            assets = assets.OrderByDescending(a => a.CreatedAt),
            shares,
        });
    }

    public async Task<(int, object?)> CreateCampaignAsync(CampaignInput input, string actorId)
    {
        if (!MarketingRules.Statuses.Contains(input.Status) || string.IsNullOrWhiteSpace(input.Title))
            return (400, "Enter a title and valid status.");

        var campaign = new MarketingCampaign { Title = input.Title.Trim(), Description = input.Description, Status = input.Status, CreatedBy = actorId };
        await campaignRepo.AddAsync(campaign);
        return (200, campaign);
    }

    public async Task<(int, object?)> UpdateCampaignAsync(string campaignId, CampaignInput input, string actorId)
    {
        var campaign = await campaignRepo.GetByIdAsync(campaignId);
        if (campaign is null) return (404, null);
        if (!MarketingRules.Statuses.Contains(input.Status) || string.IsNullOrWhiteSpace(input.Title))
            return (400, "Enter a title and valid status.");

        campaign.Title = input.Title.Trim();
        campaign.Description = input.Description;
        campaign.Status = input.Status;
        campaign.UpdatedAt = DateTime.UtcNow;
        campaign.UpdatedBy = actorId;
        await campaignRepo.UpdateAsync(campaign);
        return (200, campaign);
    }

    public async Task<(int, object?)> SavePostAsync(string campaignId, string postId, PostInput input, string actorId)
    {
        var campaign = await campaignRepo.GetByIdAsync(campaignId);
        if (campaign is null) return (404, null);
        if (campaign.Status == "Archived") return (400, "Restore the campaign before editing posts.");
        if (!MarketingRules.Statuses.Contains(input.Status) || !MarketingRules.ValidDestination(input.DestinationPath) || string.IsNullOrWhiteSpace(input.Title))
            return (400, "Check the title, status and website destination.");

        var captions = input.Captions ?? [];
        var assetIds = input.AssetIds ?? [];
        if (assetIds.Count > 12 || captions.Any(x => !MarketingRules.Channels.Contains(x.Key) || x.Value is null || x.Value.Length > 10000))
            return (400, "Check the channel captions and media selection.");

        var ids = assetIds.Distinct().ToList();
        var matchingAssetCount = await assetRepo.CountAsync(a => a.CampaignId == campaignId && ids.Contains(a.Id));
        if (matchingAssetCount != ids.Count) return (400, "Select media belonging to this campaign.");
        if (input.Status == "Ready" && (string.IsNullOrWhiteSpace(input.Caption) || ids.Count == 0))
            return (400, "Ready posts need a caption and at least one image or video.");

        var post = postId == "new" ? null : await postRepo.GetByIdAsync(postId);
        if (post is not null && post.CampaignId != campaignId) return (404, null);

        var isNew = post is null;
        post ??= new MarketingPost { CampaignId = campaignId, CreatedBy = actorId };
        post.Title = input.Title.Trim();
        post.Caption = input.Caption;
        post.Content = input.Content;
        post.DestinationPath = input.DestinationPath;
        post.UseLandingPage = input.UseLandingPage;
        post.Status = input.Status;
        post.CaptionsJson = JsonSerializer.Serialize(captions);
        post.AssetIdsJson = JsonSerializer.Serialize(ids);
        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = actorId;
        if (isNew) await postRepo.AddAsync(post);
        else await postRepo.UpdateAsync(post);

        return (200, PostDto(post));
    }

    public async Task<(int, object?)> UploadAssetAsync(string campaignId, IFormFile file, string actorId)
    {
        var campaign = await campaignRepo.GetByIdAsync(campaignId);
        if (campaign is null) return (404, null);
        if (campaign.Status == "Archived") return (400, "Restore this campaign before uploading media.");
        if (file is null || file.Length == 0) return (400, "Choose an image or video.");

        var types = new Dictionary<string, string> { ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["video/mp4"] = ".mp4", ["video/webm"] = ".webm" };
        if (!types.TryGetValue(file.ContentType, out var extension)) return (400, "Use JPEG, PNG, WebP, MP4 or WebM.");
        if (file.Length > (file.ContentType.StartsWith("video/") ? 100L : 10L) * 1024 * 1024)
            return (400, "Images can be up to 10 MB; videos up to 100 MB.");

        var header = new byte[16];
        await using (var stream = file.OpenReadStream())
            await stream.ReadAtLeastAsync(header, 12, throwOnEndOfStream: false);
        var valid = file.ContentType switch
        {
            "image/jpeg" => header[0] == 255 && header[1] == 216 && header[2] == 255,
            "image/png" => header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            "video/mp4" => System.Text.Encoding.ASCII.GetString(header, 4, 4) == "ftyp",
            "video/webm" => header.AsSpan(0, 4).SequenceEqual(new byte[] { 26, 69, 223, 163 }),
            _ => false,
        };
        if (!valid) return (400, "The file contents do not match its media type.");

        var fileName = Path.GetFileName(file.FileName);
        var asset = new MarketingAsset
        {
            CampaignId = campaignId,
            Name = fileName[..Math.Min(fileName.Length, 200)],
            ContentType = file.ContentType,
            Size = file.Length,
            CreatedBy = actorId,
        };
        asset.Url = await storage.UploadFileAsync(file, asset.Id + extension, folderName: "marketing");
        await assetRepo.AddAsync(asset);
        return (200, asset);
    }

    public async Task<(int, object?)> DeleteAssetAsync(string campaignId, string assetId, string actorId)
    {
        var asset = await assetRepo.GetByIdAsync(assetId);
        if (asset is null || asset.CampaignId != campaignId) return (404, null);

        // Drop the asset from any post that still selects it — posts are live references, unlike an
        // already-prepared MarketingShare, which freezes its own copy of the asset into SnapshotJson
        // at prepare time and is unaffected by the underlying asset being deleted afterward.
        var affectedPosts = await postRepo.GetAllAsync(p => p.CampaignId == campaignId);
        foreach (var post in affectedPosts)
        {
            var ids = JsonSerializer.Deserialize<List<string>>(post.AssetIdsJson) ?? [];
            if (!ids.Remove(assetId)) continue;
            post.AssetIdsJson = JsonSerializer.Serialize(ids);
            post.UpdatedAt = DateTime.UtcNow;
            post.UpdatedBy = actorId;
            await postRepo.UpdateAsync(post);
        }

        await assetRepo.RemoveAsync(asset);
        return (200, null);
    }

    public async Task<(int, object?)> DeletePostAsync(string campaignId, string postId, string actorId)
    {
        var post = await postRepo.GetByIdAsync(postId);
        if (post is null || post.CampaignId != campaignId) return (404, null);
        // Only a share that was actually confirmed as posted (SharedAt set) counts as real history —
        // preparing a share just generates a preview link, and blocking deletion on that alone would
        // permanently trap any post someone previewed but never confirmed sharing.
        if (await shareRepo.CountAsync(s => s.PostId == postId && s.SharedAt != null) > 0)
            return (400, "This post has share history — archive it instead of deleting it.");

        var unconfirmedShares = await shareRepo.GetAllAsync(s => s.PostId == postId);
        foreach (var share in unconfirmedShares)
        {
            await visitRepo.GetQueryable(v => v.ShareId == share.Id).ExecuteDeleteAsync();
            await shareRepo.RemoveAsync(share);
        }

        await postRepo.RemoveAsync(post);
        return (200, null);
    }

    public async Task<(int, object?)> PrepareShareAsync(string campaignId, string postId, string channel, string actorId)
    {
        var campaign = await campaignRepo.GetByIdAsync(campaignId);
        var post = await postRepo.GetByIdAsync(postId);
        if (campaign is null || post is null || post.CampaignId != campaignId) return (404, null);
        if (campaign.Status != "Ready" || post.Status != "Ready") return (400, "Mark both campaign and post Ready before preparing a public link.");
        if (!MarketingRules.Channels.Contains(channel)) return (400, "Choose a supported channel.");

        var ids = JsonSerializer.Deserialize<List<string>>(post.AssetIdsJson)!;
        var assetsById = (await assetRepo.GetAllAsync(a => a.CampaignId == campaignId && ids.Contains(a.Id))).ToDictionary(a => a.Id);
        // Preserve the post's own selection order (dictionary lookup, not the repo's arbitrary order) —
        // that order is what the public page renders media in.
        var assets = ids.Select(assetId => assetsById[assetId]).ToList();
        var captions = JsonSerializer.Deserialize<Dictionary<string, string>>(post.CaptionsJson)!;

        var share = new MarketingShare
        {
            CampaignId = campaignId,
            PostId = postId,
            Channel = channel,
            CreatedBy = actorId,
            Caption = captions.TryGetValue(channel, out var caption) && !string.IsNullOrWhiteSpace(caption) ? caption : post.Caption,
            SnapshotJson = JsonSerializer.Serialize(new MarketingSnapshot(campaign.Title, post.Title, post.Content, post.DestinationPath, post.UseLandingPage, assets)),
            ShortCode = await GenerateUniqueShortCodeAsync(),
        };
        await shareRepo.AddAsync(share);
        return (200, share);
    }

    public async Task<(int, object?)> ConfirmShareAsync(string campaignId, string shareId, string? publishedUrl, string actorId)
    {
        var share = await shareRepo.GetByIdAsync(shareId);
        if (share is null || share.CampaignId != campaignId) return (404, null);
        if (!MarketingRules.ValidPublishedUrl(publishedUrl)) return (400, "Use an HTTPS post URL, or leave it blank.");

        share.SharedAt ??= DateTime.UtcNow;
        share.PublishedUrl = string.IsNullOrWhiteSpace(publishedUrl) ? null : publishedUrl.Trim();
        share.UpdatedBy = actorId;
        share.UpdatedAt = DateTime.UtcNow;
        await shareRepo.UpdateAsync(share);
        return (200, share);
    }

    public async Task<(int, object?)> SetShareLinkStateAsync(string campaignId, string shareId, bool disabled, string actorId)
    {
        var share = await shareRepo.GetByIdAsync(shareId);
        if (share is null || share.CampaignId != campaignId) return (404, null);

        share.Disabled = disabled;
        share.UpdatedBy = actorId;
        share.UpdatedAt = DateTime.UtcNow;
        await shareRepo.UpdateAsync(share);
        return (200, null);
    }

    public async Task<(int, object?)> DeleteShareAsync(string campaignId, string shareId, string actorId)
    {
        var share = await shareRepo.GetByIdAsync(shareId);
        if (share is null || share.CampaignId != campaignId) return (404, null);
        // Same rule as DeletePostAsync — a share only becomes real history once it's actually
        // confirmed shared. An unconfirmed prepared link is just clutter from previewing/testing.
        if (share.SharedAt is not null)
            return (400, "This link has been shared — disable it instead of deleting it.");

        await visitRepo.GetQueryable(v => v.ShareId == shareId).ExecuteDeleteAsync();
        await shareRepo.RemoveAsync(share);
        return (200, null);
    }

    private async Task<string> GenerateUniqueShortCodeAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = MarketingRules.GenerateShortCode();
            if (await shareRepo.CountAsync(s => s.ShortCode == code) == 0) return code;
        }
        // Astronomically unlikely with a 7-char, 55-symbol alphabet — widen instead of looping forever.
        return MarketingRules.GenerateShortCode(12);
    }
}
