namespace ReservEase.Alumni.Platform.Api.Models;

public record PlatformMemberListItem(
    string Id, string FirstName, string LastName, string Email,
    string InstitutionId, string InstitutionName,
    string OrganizationType,
    int GraduationYear, string Status,
    DateTime? LastLoginAt, bool IsActive, DateTime CreatedAt, string? ProfilePictureUrl = null);

/// <summary>Everything the platform holds about one member, for staff support. Never includes the password or tokens.</summary>
public record PlatformMemberDetail(
    string Id, string FirstName, string LastName, string Email, string? Phone, bool IsEmailVerified,
    string InstitutionId, string InstitutionName, string OrganizationType,
    int GraduationYear, int? YearOfEntry, string? Program, string? StudentId, string? MemberNumber, string? House,
    string Status, string? BanReason, string? Company, string? JobTitle, string? Location, string EmploymentStatus,
    string? LinkedInUrl, string? Bio, string? ProfilePictureUrl, List<string>? Skills, List<string>? Interests,
    bool IsMembershipActive, DateTime? MembershipExpiry, int MembershipYearsPaid, DateTime? LastMembershipPaidAt,
    DateTime? LastLoginAt, bool IsActive, DateTime CreatedAt, DateTime? TermsAcceptedAt);

public class PlatformMemberFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public string? InstitutionId { get; set; }
    public string? Status { get; set; }
    /// <summary>true = only members who logged in within the active window; false = only members who haven't; null = everyone.</summary>
    public bool? ActiveOnly { get; set; }
}
