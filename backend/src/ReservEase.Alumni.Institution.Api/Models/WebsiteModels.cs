using Newtonsoft.Json.Linq;

namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>Where the website stands. "HasUnpublishedChanges" is true when the draft has been saved since it was last published.</summary>
public record WebsiteStatusDto(bool IsLive, DateTime? PublishedAt, string? PublishedBy, int DraftRevision, int PublishedRevision, bool HasUnpublishedChanges);

/// <summary>The institution's own branding, so the editor can show it and the website can start with it. Read from the portal theme, never edited here.</summary>
public record WebsiteBrandDto(string Name, string? Tagline, string PrimaryColor, string? SecondaryColor, string? LogoUrl, string Slug);

/// <summary>Everything the editor needs in one call. Draft is null until the institution first opens the builder.</summary>
public record WebsiteEditorDto(WebsiteStatusDto Status, JRaw? Draft, WebsiteBrandDto Brand, List<string> DisabledFeatures);

public class SaveWebsiteDraftRequest
{
    /// <summary>The whole page, as the editor holds it. The server cleans it before storing.</summary>
    public JObject? Document { get; set; }
    public int BaseRevision { get; set; }
}

public class PublishWebsiteRequest
{
    public int Revision { get; set; }
}

public record WebsiteVersionDto(string Id, int Revision, DateTime PublishedAt, string PublishedBy);

// ── What the website's live sections would show right now ─────────────────────────────────────────────────────────

public record WebsiteNewsItem(string Id, string Title, string Excerpt, string? ImageUrl, DateTime? PublishedAt, string Category);
public record WebsiteEventItem(string Id, string Title, DateTime StartDate, string Venue, string? BannerImageUrl);
public record WebsiteCommunityItem(string Id, string Name, int MemberCount, string Description, string? CoverImageUrl);
public record WebsiteBusinessItem(string Id, string BusinessName, string Location, string? LogoUrl, string? BannerUrl);
public record WebsiteServiceItem(string Id, string Name, string? Description, decimal Price, string? ImageUrl = null);
public record WebsiteProductItem(string Id, string Name, decimal Price, string? ImageUrl);
public record WebsiteFundraiserItem(string Id, string Title, string? Summary, decimal Raised, decimal Goal, string? ImageUrl);
public record WebsiteJobItem(string Id, string Title, string? Company, string? Location);
public record WebsiteSpotlightItem(string Id, string Title, string Story, string? ImageUrl, string MemberName);
public record WebsiteStats(int Members, int JoinedLast30Days, int? EventsNext30Days, int? OpenJobs, int? Businesses, int Communities);

/// <summary>
/// The institution's own items as visitors would see them on the website: the same rules as the public site (only what is public, only
/// features that are switched on). The editor shows this, so what an admin sees is what visitors will get.
/// </summary>
public record WebsiteContentDto(
    List<WebsiteNewsItem> News, List<WebsiteEventItem> Events, List<WebsiteCommunityItem> Communities, List<WebsiteBusinessItem> Businesses,
    List<WebsiteServiceItem> Services, List<WebsiteProductItem> Store, List<WebsiteFundraiserItem> Fundraisers, List<WebsiteJobItem> Jobs,
    List<WebsiteSpotlightItem> Spotlights, WebsiteStats Stats);
