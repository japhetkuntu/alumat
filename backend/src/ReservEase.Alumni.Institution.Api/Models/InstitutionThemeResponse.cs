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
    List<string> DisabledFeatures,
    string OrganizationType,
    string? CohortLabel,
    string? CohortLabelPlural,
    string GraduationYearMode = "Required",
    // Whether the platform has the WhatsApp assistant on for this institution (the server is set up for it, the platform switch is on
    // and it is not deactivated here). The institution's own on/off choice is separate and arrives in DisabledFeatures. When this is
    // false the portal shows nothing about WhatsApp at all.
    bool WhatsAppPlatformAvailable = false);
