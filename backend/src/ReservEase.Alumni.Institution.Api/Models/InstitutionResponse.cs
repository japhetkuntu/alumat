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
    string? SecondaryColorHex,
    string? InstitutionPortalTitle,
    string? InstitutionAuthHeadline,
    string? InstitutionAuthSubtext,
    string? MemberPortalTitle,
    string? MemberAuthHeadline,
    string? MemberAuthSubtext,
    bool RequireStudentId,
    bool ProgramOfStudyEnabled,
    List<string> ProgramsOfStudy,
    Dictionary<string, string> SocialLinks,
    string MemberActivePolicy,
    bool PromptMembershipActivationAtSignup,
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled,
    List<string> DisabledFeatures,
    List<LandingPageStory> LandingPageStories,
    NewsBanner? NewsBanner,
    List<string> HeroImageUrls,
    string? HeroHeadline,
    string Status,
    // Shareable Member Portal URL for this institution — null if MemberPortalBaseDomain isn't configured.
    string? MemberPortalUrl,
    // Live settlement details — null until PayoutStatus is Approved.
    string PayoutStatus,
    string? SettlementBankName,
    string? SettlementAccountNumber,
    string? SettlementAccountName);

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
    public List<string> HeroImageUrls { get; set; } = [];
    public string? HeroHeadline { get; set; }
}

/// <summary>
/// Institution SuperAdmins editing their own branding: display name,
/// tagline, support email, colors, logos/icons, and both portals' auth
/// copy. Mirrors Platform.Api's UpdateInstitutionBrandingRequest field for
/// field, minus ContactEmail (the institution's own legal/contact address,
/// not a branding choice) and RequireStudentId (a signup policy, not
/// branding). Takes effect immediately, same as the other me/* endpoints.
/// </summary>
public class UpdateInstitutionBrandingRequest
{
    public string PortalName { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? SupportEmail { get; set; }
    public string? LogoUrl { get; set; }
    public string? IconUrl { get; set; }
    public string PrimaryColorHex { get; set; } = string.Empty;
    public string? SecondaryColorHex { get; set; }
    public string? InstitutionPortalTitle { get; set; }
    public string? InstitutionAuthHeadline { get; set; }
    public string? InstitutionAuthSubtext { get; set; }
    public string? MemberPortalTitle { get; set; }
    public string? MemberAuthHeadline { get; set; }
    public string? MemberAuthSubtext { get; set; }
}

/// <summary>
/// Another institution-editable-themselves carve-out — how "active member"
/// status is determined, and whether a student ID is required at
/// registration, are this institution's own operational choices, not
/// platform staff's, so they're edited here rather than via Platform.Api.
/// </summary>
public record UpdateMemberActivePolicyRequest(string MemberActivePolicy, bool RequireStudentId);

/// <summary>
/// Program/Course of Study collection at registration — off by default. The
/// list is this institution's own admin-managed dropdown; members can still
/// type a custom value at registration when their program isn't listed, so
/// the list never needs to be exhaustive on day one.
/// </summary>
public record UpdateProgramOfStudyRequest(bool ProgramOfStudyEnabled, List<string> ProgramsOfStudy);

/// <summary>
/// The institution's own social media profile URLs, shown as icon links in
/// the Member Portal's footer. Keys are validated against
/// SocialLinkPlatforms.All — an unrecognized key is rejected rather than
/// silently stored, so the frontend's fixed icon set never has to guess
/// what an arbitrary key means.
/// </summary>
public record UpdateSocialLinksRequest(Dictionary<string, string> SocialLinks);

public static class SocialLinkPlatforms
{
    public const string Facebook = "facebook";
    public const string Twitter = "twitter";
    public const string Instagram = "instagram";
    public const string LinkedIn = "linkedin";
    public const string Youtube = "youtube";
    public const string Tiktok = "tiktok";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Facebook, Twitter, Instagram, LinkedIn, Youtube, Tiktok,
    };
}

/// <summary>
/// Another institution-editable-themselves carve-out, distinct from the
/// platform-only bulk feature toggle (Platform.Api's InstitutionsController).
/// DigestEnabled/RecurringGivingEnabled toggle the two DisabledFeatures keys
/// in InstitutionFeatures.SelfService — everything else in that list stays
/// platform-staff-only. PromptMembershipActivationAtSignup,
/// EmailNotificationsEnabled, and SmsNotificationsEnabled are separate
/// dedicated fields (not DisabledFeatures keys): the first is opt-in
/// (default false), the latter two are opt-out (default true, since the
/// notifications already send today and turning them off is a deliberate
/// cost decision, not a new capability being unlocked).
/// </summary>
public record UpdateSelfServiceFeaturesRequest(
    bool DigestEnabled,
    bool RecurringGivingEnabled,
    bool PromptMembershipActivationAtSignup,
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled,
    bool BirthdaySpotlightEnabled);

/// <summary>
/// Submitted by this institution's own SuperAdmin only (unlike a batch's
/// payout setup, a ScopedAdmin may never touch the institution's own payment
/// info). Never writes the live settlement fields directly — lands in
/// Institution.PendingPayoutChanges until a platform staffer approves it
/// (see Platform.Api's InstitutionPayoutsController).
/// </summary>
public class SubmitInstitutionPayoutSetupRequest
{
    public string SettlementBankCode { get; set; } = string.Empty;
    public string SettlementBankName { get; set; } = string.Empty;
    public string SettlementAccountNumber { get; set; } = string.Empty;
    public string SettlementAccountName { get; set; } = string.Empty;
}
