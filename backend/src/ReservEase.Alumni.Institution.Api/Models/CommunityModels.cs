using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Institution.Api.Models;

public record CommunityListItem(
    string Id, string Name, string? Description, string? CoverImageUrl, bool IsActive,
    int ApprovedCount, int PendingCount, int LeaderCount, DateTime CreatedAt,
    List<CommunityChannelItem>? Channels = null, int JoinedViaInvites = 0, int JoinedLast30Days = 0);

public record CommunityChannelItem(string Type, string DisplayName, string? InviteUrl, DateTime ConnectedAt);

public record CommunityChannelInputItem(string? Type, string? DisplayName, string? InviteUrl);

/// <summary>How members have been joining through invitations, across the institution.</summary>
public record CommunityGrowthSummary(int TotalViaInvites, int Last30Days, List<GrowthSourceItem> BySource, List<GrowthCommunityItem> ByCommunity);
public record GrowthSourceItem(string Source, int Count);
public record GrowthCommunityItem(string CommunityId, string Name, int Joined, int Last30Days);

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

    /// <summary>Connected external groups. Null leaves them unchanged; an empty list disconnects them all.</summary>
    public List<CommunityChannelInputItem>? ExternalChannels { get; set; }
}

public class SetCommunityMemberRoleRequest
{
    /// <summary>"Member" or "Leader".</summary>
    [Required]
    public string Role { get; set; } = "Member";
}
