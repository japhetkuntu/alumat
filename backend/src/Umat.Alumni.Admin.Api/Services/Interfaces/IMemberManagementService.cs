using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IMemberManagementService
{
    Task<IApiResponse<PgPagedResult<MemberListItem>>> GetMembersAsync(MemberListFilter filter, AuthData admin);
    Task<IApiResponse<MemberDetailItem>> GetMemberByIdAsync(string memberId, AuthData admin);
    Task<IApiResponse<object>> ApproveMemberAsync(string memberId, AuthData admin);
    Task<IApiResponse<object>> RejectMemberAsync(string memberId, string? reason, AuthData admin);
    Task<IApiResponse<object>> BanMemberAsync(string memberId, string? reason, AuthData admin);
    Task<IApiResponse<object>> UnbanMemberAsync(string memberId, AuthData admin);
    Task<IApiResponse<ImportMembersResult>> ImportMembersAsync(ImportMembersRequest request, AuthData admin);
    Task<IApiResponse<object>> ActivateMembershipAsync(string memberId, ActivateMembershipRequest request, AuthData admin);
}

public record ImportMembersResult(int Imported, int Skipped, List<string> Errors);

public record MemberListItem(
    string Id, string FirstName, string LastName, string Email,
    string? Phone, int GraduationYear, string DepartmentId,
    string Status, string? Company, DateTime CreatedAt,
    string? MemberNumber, bool IsEmailVerified, int RejectionCount, string? ProfilePictureUrl,
    bool IsMembershipActive, DateTime? MembershipExpiry);

public record MemberDetailItem(
    string Id, string FirstName, string LastName, string Email,
    string? Phone, int GraduationYear, string DepartmentId,
    string Status, string? Company, string? JobTitle, string? Location,
    string? LinkedInUrl, string? Bio, string? ProfilePictureUrl,
    string? StudentId, DateTime CreatedAt, DateTime? LastLoginAt,
    string? MemberNumber, bool IsEmailVerified, int RejectionCount, string? BanReason,
    bool IsMembershipActive, DateTime? MembershipExpiry, int MembershipYearsPaid, DateTime? LastMembershipPaidAt);
