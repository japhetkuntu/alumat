using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Member.Api.Models;

public record InstitutionThemeResponse(
    string Slug,
    string PortalName,
    string DisplayName,
    string PrimaryColorHex,
    string? SecondaryColorHex,
    string? LogoUrl,
    string? Tagline,
    string? IconUrl,
    string? PortalTitle,
    string? AuthHeadline,
    string? AuthSubtext,
    bool RequireStudentId,
    List<string> DisabledFeatures,
    List<LandingPageStory> LandingPageStories,
    NewsBanner? NewsBanner,
    string? HeroImageUrl,
    string? HeroHeadline);
