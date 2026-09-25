using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Member : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? StudentId { get; set; }
    public int GraduationYear { get; set; }
    // Opt-in — null unless the member has entered it themselves. Only the
    // month/day are ever used (the Birthday Spotlight scheduler), so the
    // year carries no real significance beyond what DateTime requires.
    public DateTime? DateOfBirth { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    /// <summary>
    /// Program/course of study, e.g. "BSc Mining Engineering" — always stored
    /// as freeform text regardless of whether it came from the registration
    /// form's dropdown (Institution.ProgramsOfStudy) or a typed custom value.
    /// Null when the institution doesn't collect this (ProgramOfStudyEnabled).
    /// </summary>
    public string? Program { get; set; }
    public string? Company { get; set; }
    public string? JobTitle { get; set; }
    public string? Location { get; set; }
    // Opt-in: member must explicitly choose to appear on the Alumni Map. Plotted by
    // MapLatitude/MapLongitude (captured from the browser's geolocation API, not
    // guessed from the free-text Location above) — rounded to ~11km precision
    // (1 decimal degree) both client- and server-side before storage, so the pin
    // reads as "roughly this city/region" rather than the member's exact address.
    // Both null whenever ShowOnAlumniMap is false — cleared on opt-out rather than
    // just hidden, so no location data lingers once a member turns this off.
    public bool ShowOnAlumniMap { get; set; }
    public double? MapLatitude { get; set; }
    public double? MapLongitude { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Bio { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string Status { get; set; } = "Pending";  // Pending, Active, Suspended, Banned, Blocked
    public DateTime? LastLoginAt { get; set; }
    // Email verification
    public bool IsEmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationSentAt { get; set; }
    // Rejection tracking (max 3 rejections before blocked)
    public int RejectionCount { get; set; }
    // Unique readable member number (e.g. GREENFIELD-2021-0001), assigned on approval
    public string? MemberNumber { get; set; }
    // Ban reason for active members
    public string? BanReason { get; set; }

    // Employment status: Employed, Pensioner
    public string EmploymentStatus { get; set; } = "Employed";

    // Membership renewal tracking
    public bool IsMembershipActive { get; set; } = false;
    public DateTime? MembershipExpiry { get; set; }
    public int MembershipYearsPaid { get; set; }
    public DateTime? LastMembershipPaidAt { get; set; }

    // Referral program
    public string? ReferralCode { get; set; }
    public string? ReferredById { get; set; }

    // School records — all opt-in, member-entered enrichment fields
    public int? YearOfEntry { get; set; }
    // Freeform for now, same convention as Program (no Institution.Houses list exists yet)
    public string? House { get; set; }
    // Day, Boarding
    public string? StudentStatus { get; set; }
    public string? PrefectStatus { get; set; }
    public List<string>? ClubsAndSocieties { get; set; }
    public List<string>? LeadershipRoles { get; set; }
    public string? Achievements { get; set; }

    /// <summary>When the member accepted the Terms of Service and Privacy Policy and confirmed they are 18 or older.</summary>
    public DateTime? TermsAcceptedAt { get; set; }

    public string? ConnectionType { get; set; }
    public List<string>? Skills { get; set; }
    public List<string>? Interests { get; set; }
    public bool ShowEmailOnDirectory { get; set; } = false;
    public bool ShowPhoneOnDirectory { get; set; } = false;
    public bool ShowCompanyOnDirectory { get; set; } = true;
    public bool ShowBioOnDirectory { get; set; } = true;
}
