namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>Institution-wide (unscoped) news for the public landing page — never includes community/year-restricted posts, since an anonymous visitor has no membership context to check against.</summary>
public record PublicNewsItemResponse(string Id, string Title, string Excerpt, string? ImageUrl, DateTime? PublishedAt, string Category);

public record PublicEventItemResponse(string Id, string Title, DateTime StartDate, string Venue, string? BannerImageUrl);

public record PublicSpotlightItemResponse(string Id, string Title, string Story, string? ImageUrl, string MemberName, DateTime? FeaturedMonth);
public record PublicBusinessListingItemResponse(string Id, string BusinessName, string Description, string? LogoUrl, string? BannerUrl, string Location, string? WebsiteUrl, string? ExternalLinkUrl);
