namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>Institution-wide (unscoped) news for the public landing page — never includes community/year-restricted posts, since an anonymous visitor has no membership context to check against.</summary>
public record PublicNewsItemResponse(string Id, string Title, string Excerpt, string? ImageUrl, DateTime? PublishedAt, string Category);

public record PublicEventItemResponse(string Id, string Title, DateTime StartDate, string Venue, string? BannerImageUrl);

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
