using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Institution.Api.Models;

public record CommunityListItem(
    string Id, string Name, string? Description, string? CoverImageUrl, bool IsActive,
    int ApprovedCount, int PendingCount, int LeaderCount, DateTime CreatedAt);

public record CommunityMemberItem(
    string MembershipId, string MemberId, string MemberName, string MemberEmail,
    string Role, string Status, DateTime RequestedAt);

public class CreateCommunityRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string? CoverImageUrl { get; set; }
}

public class UpdateCommunityRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SetCommunityMemberRoleRequest
{
    /// <summary>"Member" or "Leader".</summary>
    [Required]
    public string Role { get; set; } = "Member";
}
