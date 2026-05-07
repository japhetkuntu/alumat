namespace Umat.Alumni.Member.Api.Models;

public record MemberProfileResponse(
    string Id, string FirstName, string LastName, string Email,
    string? Phone, string? MemberNumber, int GraduationYear,
    string DepartmentId, string? Company, string? JobTitle,
    string? Location, string? LinkedInUrl, string? Bio,
    string? ProfilePictureUrl, string Status, string EmploymentStatus,
    bool IsMembershipActive, DateTime? MembershipExpiry, int MembershipYearsPaid, DateTime? LastMembershipPaidAt);
