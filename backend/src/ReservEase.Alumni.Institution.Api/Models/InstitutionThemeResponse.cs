namespace ReservEase.Alumni.Institution.Api.Models;

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
    List<string> DisabledFeatures);
