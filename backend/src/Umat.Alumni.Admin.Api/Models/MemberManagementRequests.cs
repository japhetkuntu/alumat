namespace Umat.Alumni.Admin.Api.Models;

public record ApproveMemberRequest(string MemberId);
public record RejectMemberRequest(string MemberId, string? Reason);
public record BanMemberRequest(string MemberId, string? Reason);

public record ImportMemberItem(
    string FirstName, string LastName, string Email, string? Phone,
    string? StudentId, int GraduationYear, string? DepartmentId,
    List<int>? PaidMembershipYears);

public record ImportMembersRequest(List<ImportMemberItem> Members);

public record ActivateMembershipRequest(List<int> MembershipYears);
