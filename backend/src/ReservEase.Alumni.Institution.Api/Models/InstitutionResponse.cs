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
/// Another institution-editable-themselves carve-out — how "active member"
/// status is determined is this institution's own operational choice, not
/// platform staff's, so it's edited here rather than via Platform.Api.
/// </summary>
public record UpdateMemberActivePolicyRequest(string MemberActivePolicy);

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
    bool SmsNotificationsEnabled);

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
