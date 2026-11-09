namespace ReservEase.Alumni.Member.Api.Models;

public record LeadershipPostDto(
    string Id, string MemberId, string MemberName, string? ProfilePictureUrl, string? JobTitle,
    string Title, string ScopeType, string ScopeId, string ScopeName);

public record PrivilegeDto(string Key, string Label, string Description);

/// <summary>A role the signed-in member holds: where it applies and, in plain words, what it lets them do.</summary>
public record MyRoleDto(string PostId, string RoleName, string ScopeType, string ScopeId, string ScopeName, List<PrivilegeDto> Privileges);
