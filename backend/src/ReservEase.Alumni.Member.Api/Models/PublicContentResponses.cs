namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>Institution-wide (unscoped) news for the public landing page — never includes community/year-restricted posts, since an anonymous visitor has no membership context to check against.</summary>
public record PublicNewsItemResponse(string Id, string Title, string Excerpt, string? ImageUrl, DateTime? PublishedAt, string Category, bool IsPublic = false);

public record PublicEventItemResponse(string Id, string Title, DateTime StartDate, string Venue, string? BannerImageUrl, bool IsPublic = false);

public record PublicSpotlightItemResponse(string Id, string Title, string Story, string? ImageUrl, string MemberName, DateTime? FeaturedMonth);
public record PublicBusinessListingItemResponse(string Id, string BusinessName, string Description, string? LogoUrl, string? BannerUrl, string Location, string? WebsiteUrl, string? ExternalLinkUrl);

/// <summary>Aggregate, non-personal counts for the public landing page. A null figure means that feature is off for this institution.</summary>
public record PublicPulseResponse(int Members, int JoinedLast30Days, int? EventsNext30Days, DateTime? NextEventDate, int? OpenJobs, int? Businesses);

/// <summary>
/// What a shared referral link opens to, before the visitor has signed up for anything — the
/// "no-login preview moment": something real and specific about THIS invite (who sent it, how
/// many of their own batch are already here, what's live right now) rather than a cold signup
/// form or a locked-out landing page. Null fields mean that data point doesn't apply (e.g. no
/// fundraiser currently open) rather than the feature being off — unlike PublicPulseResponse,
/// there's no per-institution feature flag here to distinguish "off" from "empty."
/// </summary>
public record ReferralPreviewResponse(
    string ReferrerFirstName,
    int? ReferrerGraduationYear,
    int SameBatchActiveMembers,
    int TotalActiveMembers,
    string? OpenFundraiserTitle,
    decimal? OpenFundraiserCollected,
    decimal? OpenFundraiserTarget,
    string? CommunityName = null);

/// <summary>A group the institution chose to show on its public landing page. Counts only, never members.</summary>
public record PublicCommunityItemResponse(string Id, string Name, string? Kind, string Description, string? CoverImageUrl, int MemberCount);

// ── The institution's website (see PublicController.GetPublicSiteData) ───────────────────────────────

public record PublicServiceItemResponse(string Id, string Name, string? Description, decimal Price);
public record PublicProductItemResponse(string Id, string Name, decimal Price, string? ImageUrl);
public record PublicFundraiserItemResponse(string Id, string Title, string? Summary, decimal Raised, decimal Goal, string? ImageUrl);
public record PublicJobItemResponse(string Id, string Title, string? Company, string? Location);
public record PublicSiteStatsResponse(int Members, int JoinedLast30Days, int? EventsNext30Days, int? OpenJobs, int? Businesses, int Communities);

/// <summary>
/// Everything the live sections of an institution's website show, in one call. Each list holds only what the institution made public, and is
/// empty when its feature is switched off, so the website shows nothing it should not.
/// </summary>
public record PublicSiteDataResponse(
    List<PublicNewsItemResponse> News,
    List<PublicEventItemResponse> Events,
    List<PublicCommunityItemResponse> Communities,
    List<PublicBusinessListingItemResponse> Businesses,
    List<PublicServiceItemResponse> Services,
    List<PublicProductItemResponse> Store,
    List<PublicFundraiserItemResponse> Fundraisers,
    List<PublicJobItemResponse> Jobs,
    PublicSiteStatsResponse Stats);
