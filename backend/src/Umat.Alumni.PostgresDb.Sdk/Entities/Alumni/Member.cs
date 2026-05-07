namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Member : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? StudentId { get; set; }
    public int GraduationYear { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? JobTitle { get; set; }
    public string? Location { get; set; }
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
    // Unique readable member number (e.g. UMaT/CS/2021/0001), assigned on approval
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
}

