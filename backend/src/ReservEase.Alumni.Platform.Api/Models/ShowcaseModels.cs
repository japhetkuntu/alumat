using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>One institution as the website showcase sees it: how it is set up, and the numbers that would be shown, counted live.</summary>
public record ShowcaseEntry(
    string InstitutionId, string Name, string Slug, string? LogoUrl, string? Tagline, string Status, string OrganizationType, string? PortalUrl,
    bool Featured, string? Blurb, int Order, List<string> Stats, DateTime? FeaturedAt, string? FeaturedBy, ShowcaseNumbers Numbers);

/// <param name="PermissionConfirmed">Required to feature: staff confirm the institution has agreed to be named on the website.</param>
public record SaveShowcaseRequest(bool Featured, string? Blurb, int Order, List<string>? Stats, bool PermissionConfirmed);
