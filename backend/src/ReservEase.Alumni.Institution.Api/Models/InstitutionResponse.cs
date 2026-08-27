using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Institution.Api.Models;

public record InstitutionResponse(
    string Id,
    string Name,
    string Slug,
    string? CustomDomain,
    string PortalName,
    string? Tagline,
    string ContactEmail,
    string? SupportEmail,
    string? LogoUrl,
    string? IconUrl,
    string PrimaryColorHex,
    string? InstitutionPortalTitle,
    string? InstitutionAuthHeadline,
    string? InstitutionAuthSubtext,
    string? MemberPortalTitle,
    string? MemberAuthHeadline,
    string? MemberAuthSubtext,
    bool RequireStudentId,
    string MemberActivePolicy,
    List<string> DisabledFeatures,
    List<LandingPageStory> LandingPageStories,
    NewsBanner? NewsBanner,
    string Status,
    // Shareable Member Portal URL for this institution — null if MemberPortalBaseDomain isn't configured.
    string? MemberPortalUrl);

/// <summary>
/// Deliberate carve-out from the "institution staff can't edit branding"
/// policy (see <see cref="Controllers.InstitutionController"/>) — the Member
/// Portal landing page's Stories and news banner are meant to be editable by
/// the institution's own admins, not just platform staff.
/// </summary>
public class UpdateLandingContentRequest
{
    public List<LandingPageStory> LandingPageStories { get; set; } = [];
    public NewsBanner? NewsBanner { get; set; }
}

/// <summary>
/// Another institution-editable-themselves carve-out — how "active member"
/// status is determined is this institution's own operational choice, not
/// platform staff's, so it's edited here rather than via Platform.Api.
/// </summary>
public record UpdateMemberActivePolicyRequest(string MemberActivePolicy);
