namespace ReservEase.Alumni.Institution.Api.Models;

public record ApproveMemberRequest(string MemberId);
public record RejectMemberRequest(string MemberId, string? Reason);
public record BanMemberRequest(string MemberId, string? Reason);

public record ImportMemberItem(
    string FirstName, string LastName, string Email, string? Phone,
    string? StudentId, int? GraduationYear, string? DepartmentId,
    List<int>? PaidMembershipYears,
    // Answers to the community's own registration questions, keyed by question id. Anything not matching a question is dropped.
    Dictionary<string, string>? CustomFields = null);

public record ImportMembersRequest(List<ImportMemberItem> Members);

public record ActivateMembershipRequest(List<int> MembershipYears);

/// <summary>Answers to the community's own registration questions, replaced as a whole. Keyed by question id.</summary>
public record UpdateMemberCustomFieldsRequest(Dictionary<string, string>? Answers);
