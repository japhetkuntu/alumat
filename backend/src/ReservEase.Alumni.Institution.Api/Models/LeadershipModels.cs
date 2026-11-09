using System.ComponentModel.DataAnnotations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Institution.Api.Models;

public record PrivilegeItem(string Key, string Label, string Description);

public record LeadershipRoleItem(string Id, string Name, List<string> Privileges, int HolderCount);

public class SaveLeadershipRoleRequest
{
    [Required, MaxLength(60)] public string Name { get; set; } = string.Empty;
    /// <summary>Keys from the privilege list; none means recognition only.</summary>
    public List<string> Privileges { get; set; } = [];
}

public record LeadershipPostItem(
    string Id, string MemberId, string MemberName, string? ProfilePictureUrl, int GraduationYear,
    string RoleId, string Title, List<string> Privileges, string ScopeType, string ScopeId, string ScopeName, DateTime CreatedAt);

public class AppointLeaderRequest
{
    [Required] public string MemberId { get; set; } = string.Empty;
    [Required] public string RoleId { get; set; } = string.Empty;
    /// <summary>Institution, Batch or Community.</summary>
    [Required] public string ScopeType { get; set; } = string.Empty;
    /// <summary>The year group's or community's id; left out for the whole institution.</summary>
    public string? ScopeId { get; set; }
}

public class ChangeLeaderRoleRequest
{
    [Required] public string RoleId { get; set; } = string.Empty;
}

public record LeadershipSettingsItem(int GroupMessagesPerDay, int MaxGroupMessagesPerDay);

public class SaveLeadershipSettingsRequest
{
    /// <summary>How many messages one leader may send to the same group in a day.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, GroupMessaging.MaxPerDay)] public int GroupMessagesPerDay { get; set; }
}
