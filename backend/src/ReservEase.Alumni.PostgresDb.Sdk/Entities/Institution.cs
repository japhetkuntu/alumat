namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A tenant of the platform — one client institution (e.g. a university).</summary>
public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Subdomain slug, e.g. "greenfield" for greenfield.{PlatformBaseDomain}. Unique, lowercase.</summary>
    public string Slug { get; set; } = string.Empty;
    public string? CustomDomain { get; set; }

    // Branding — editable by platform staff only (InstitutionsController), never
    // by the institution's own staff (institution-side branding endpoints are
    // read-only; see InstitutionController.GetMe in Institution.Api).
    public string PortalName { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? SupportEmail { get; set; }
    public string? LogoUrl { get; set; }
    /// <summary>Small icon/favicon — distinct from LogoUrl, used for browser tabs and app icons.</summary>
    public string? IconUrl { get; set; }
    public string PrimaryColorHex { get; set; } = "#2563eb";
    /// <summary>
    /// Optional second brand color. Many institutions have a real two-color
    /// identity (e.g. navy + gold) — when set, the shared palette algorithm
    /// (see generateBrandPalette / EmailColorPalette, which must stay in sync)
    /// derives the accent family from this instead of a flat generic gold.
    /// Null is fine — every institution worked before this field existed.
    /// </summary>
    public string? SecondaryColorHex { get; set; }

    // Per-portal content — lets platform staff customize what each institution's
    // audience sees, without any code change ("plug and play" configuration).
    public string? InstitutionPortalTitle { get; set; }
    public string? InstitutionAuthHeadline { get; set; }
    public string? InstitutionAuthSubtext { get; set; }
    public string? MemberPortalTitle { get; set; }
    public string? MemberAuthHeadline { get; set; }
    public string? MemberAuthSubtext { get; set; }

    /// <summary>Whether member self-registration requires a Student ID. Defaults to true (most institutions track one).</summary>
    public bool RequireStudentId { get; set; } = true;

    /// <summary>
    /// Whether member self-registration collects a Program/Course of Study —
    /// off by default, since not every institution's programs map cleanly
    /// onto Department (a Department can offer several distinct programs).
    /// When on, ProgramsOfStudy below drives a dropdown on the registration
    /// form; the member can still type a custom value when their program
    /// isn't listed (see RegisterRequest.Program — always freeform text on
    /// the wire either way, the dropdown is purely a frontend convenience).
    /// </summary>
    public bool ProgramOfStudyEnabled { get; set; }

    /// <summary>The institution's own admin-managed list of selectable programs — see ProgramOfStudyEnabled.</summary>
    public List<string> ProgramsOfStudy { get; set; } = [];

    /// <summary>
    /// Whether the just-registered member sees an "Activate your membership"
    /// prompt (pay the current membership campaign, or a "nothing due yet"
    /// notice) on the registration success screen. Defaults to false — most
    /// institutions want a clean "you're pending approval" screen and would
    /// rather not push a payment ask before a member is even approved; an
    /// institution opts into it deliberately via InstitutionController's
    /// self-service settings.
    /// </summary>
    public bool PromptMembershipActivationAtSignup { get; set; } = false;

    /// <summary>
    /// Whether this institution sends outbound notification emails — digest,
    /// referral invitations, and any future non-transactional email. Defaults
    /// to true (preserves existing behavior). Never gates authentication or
    /// signup email (OTP, email-verification link, password reset, staff
    /// invite) — those always send regardless, since a member who can't
    /// receive them can't get into their account at all.
    /// </summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Whether this institution sends outbound SMS — per-member alerts
    /// (gated further by each member's own NotificationPreference.SmsAlerts)
    /// and admin broadcasts. Off by default would silently break existing
    /// institutions relying on it, so this defaults to true; an institution
    /// watching its Arkesel bill turns it off deliberately. SMS is a real
    /// per-message cost, unlike email, so this is the one most worth having
    /// turned off intentionally rather than left on by accident. Never gates
    /// authentication or signup SMS (there is none today — OTP/verification
    /// is email-only).
    /// </summary>
    public bool SmsNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// How "active member" status is determined for this institution.
    /// "ApprovedOnly" (default) — any approved member (Member.Status ==
    /// "Active") counts as active regardless of dues. "DuesRequired" — a
    /// member is active only once current and past required dues are paid;
    /// institutions opt into this intentionally. Editable by the institution's
    /// own admins (InstitutionController.UpdateMemberActivePolicy) or by
    /// platform staff at onboarding/on the institution's profile
    /// (Platform.Api's InstitutionsController).
    /// </summary>
    public string MemberActivePolicy { get; set; } = "ApprovedOnly";

    /// <summary>
    /// Product features platform staff have turned off for this institution — see
    /// <see cref="InstitutionFeatures"/> for the canonical key list. Empty by
    /// default, meaning every feature is enabled; adding a key here disables it.
    /// Enforced both in each portal's nav (hidden) and server-side (see
    /// RequireFeatureAttribute) so a disabled feature is actually inaccessible,
    /// not just hidden from view.
    /// </summary>
    public List<string> DisabledFeatures { get; set; } = [];

    /// <summary>
    /// "Why alumni join" cards on the Member Portal's public landing page.
    /// Empty by default — the frontend falls back to generic built-in copy
    /// until the institution (or platform staff) configures its own.
    /// Editable by both platform staff and this institution's own admins.
    /// </summary>
    public List<LandingPageStory> LandingPageStories { get; set; } = [];

    /// <summary>
    /// The dismissible announcement strip at the top of the Member Portal's
    /// landing page. Null/disabled by default — no banner shown until
    /// configured. Editable by both platform staff and this institution's
    /// own admins.
    /// </summary>
    public NewsBanner? NewsBanner { get; set; }

    /// <summary>
    /// Overrides the Member Portal landing page's hero photo(s) — one or more
    /// images shown as a carousel. Empty by default — the frontend falls back
    /// to a generic stock photo until the institution (or platform staff)
    /// uploads their own. Editable by both platform staff and this
    /// institution's own admins.
    /// </summary>
    public List<string> HeroImageUrls { get; set; } = [];

    /// <summary>
    /// Overrides the short headline overlaid on the landing page's hero
    /// photo (default: "One network. Every graduate, wherever they are.").
    /// Editable by both platform staff and this institution's own admins.
    /// </summary>
    public string? HeroHeadline { get; set; }

    // Active or Suspended — a suspended institution's staff and members are
    // locked out of login entirely, and every institution-scoped request is
    // rejected except the Paystack payment callback (see
    // TenantResolutionMiddleware), so in-flight transactions still settle.
    public string Status { get; set; } = "Active";
    public DateTime? TrialEndsAt { get; set; }
    public DateTime OnboardedAt { get; set; } = DateTime.UtcNow;

    // ── Revenue & payment split ──────────────────────────────────────────
    /// <summary>
    /// The platform's cut of every successful online payment this institution
    /// collects, e.g. 5.00 = 5%. Set explicitly at onboarding — no silent
    /// default, a platform admin must choose it per institution.
    /// </summary>
    public decimal PlatformFeePercentage { get; set; }

    /// <summary>
    /// Above this payment amount, the flat fee below replaces the percentage
    /// cut entirely — not a top-up. E.g. threshold 200, flat fee 15: a ₵150
    /// payment pays 5% (₵7.50), a ₵1,000 payment pays a flat ₵15, not
    /// ₵50. Null (with <see cref="PlatformFeeFlatAmount"/>) means pure
    /// percentage pricing, unchanged from before this existed.
    /// </summary>
    public decimal? PlatformFeeFlatThreshold { get; set; }

    /// <summary>The flat fee charged once <see cref="PlatformFeeFlatThreshold"/> is exceeded. Both must be set together for tiered pricing to apply.</summary>
    public decimal? PlatformFeeFlatAmount { get; set; }

    /// <summary>Paystack subaccount code once created — null until settlement banking details are set.</summary>
    public string? PaystackSubaccountCode { get; set; }
    public string? SettlementBankCode { get; set; }
    /// <summary>Display name of the bank — Paystack itself only needs the code, this is for UI.</summary>
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }

    /// <summary>
    /// None (never submitted) — Pending (the institution's own SuperAdmin
    /// submitted settlement details via InstitutionController's self-service
    /// payout-setup endpoint, awaiting platform review) — Approved (the
    /// settlement fields above are live) — Rejected (platform staff declined;
    /// the previous live settlement fields, if any, are unchanged). Platform
    /// staff editing settlement fields directly (Platform.Api's
    /// InstitutionsController.UpdatePaymentsAsync) always leaves this
    /// Approved — that path is trusted by definition, no review needed.
    /// </summary>
    public string PayoutStatus { get; set; } = "None";

    /// <summary>
    /// Proposed values from the institution's most recent self-service
    /// payout-setup submission, staged here until a platform staffer
    /// approves or rejects them — the live fields above are only ever
    /// written by that approval (or by platform staff directly), never by
    /// the institution-side submission endpoint. Cleared on either approve
    /// or reject.
    /// </summary>
    public InstitutionPayoutPendingChanges? PendingPayoutChanges { get; set; }

    /// <summary>
    /// Fallback delivery/pickup instructions for new Store products — a
    /// product's own <c>DeliveryInfo</c> is used when set, otherwise this is
    /// shown to buyers. Saves institution staff from retyping the same
    /// pickup instructions on every product. Null until an admin sets one.
    /// </summary>
    public string? DefaultStoreDeliveryInfo { get; set; }

    /// <summary>
    /// Ordered list of delivery-status stage names an admin has defined for
    /// this institution's Store (e.g. ["Processing","Shipped","Delivered"]).
    /// Empty by default — no forced seed values — so an institution that
    /// doesn't want delivery tracking just leaves this empty and the whole
    /// feature stays invisible (StoreOrder.DeliveryStatus is never set).
    /// </summary>
    public List<string> StoreDeliveryStages { get; set; } = [];
}

public class InstitutionPayoutPendingChanges
{
    public string? SettlementBankCode { get; set; }
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }
}
