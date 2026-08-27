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

    public string Status { get; set; } = "Trial";  // Trial, Active, Suspended, Cancelled
    public DateTime? TrialEndsAt { get; set; }
    public DateTime OnboardedAt { get; set; } = DateTime.UtcNow;

    // ── Revenue & payment split ──────────────────────────────────────────
    /// <summary>
    /// The platform's cut of every successful online payment this institution
    /// collects, e.g. 5.00 = 5%. Set explicitly at onboarding — no silent
    /// default, a platform admin must choose it per institution.
    /// </summary>
    public decimal PlatformFeePercentage { get; set; }

    /// <summary>Paystack subaccount code once created — null until settlement banking details are set.</summary>
    public string? PaystackSubaccountCode { get; set; }
    public string? SettlementBankCode { get; set; }
    /// <summary>Display name of the bank — Paystack itself only needs the code, this is for UI.</summary>
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }
}
