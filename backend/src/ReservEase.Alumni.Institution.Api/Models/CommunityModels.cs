using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Institution.Api.Models;

public record CommunityListItem(
    string Id, string Name, string? Description, string? CoverImageUrl, bool IsActive,
    int ApprovedCount, int PendingCount, int LeaderCount, DateTime CreatedAt,
    int JoinedViaInvites = 0, int JoinedLast30Days = 0, string? Kind = null, bool IsPublic = false,
    string PayoutStatus = "None", bool UseInstitutionAccount = false,
    string? SettlementBankName = null, string? SettlementAccountNumber = null, string? SettlementAccountName = null);

/// <summary>How members have been joining through invitations, across the institution.</summary>
public record CommunityGrowthSummary(int TotalViaInvites, int Last30Days, List<GrowthSourceItem> BySource, List<GrowthCommunityItem> ByCommunity);
public record GrowthSourceItem(string Source, int Count);
public record GrowthCommunityItem(string CommunityId, string Name, int Joined, int Last30Days);

public record CommunityMemberItem(
    string MembershipId, string MemberId, string MemberName, string MemberEmail,
    string Role, string Status, DateTime RequestedAt);

/// <summary>An administrator's decision on one member of a group. The reason is optional and is shown to the member.</summary>
public class CommunityMemberDecisionRequest
{
    [MaxLength(300)]
    public string? Reason { get; set; }
}

public class CreateCommunityRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(ReservEase.Alumni.Common.Sdk.Text.RichText.MaxHtmlLength)]
    public string? Description { get; set; }

    public string? CoverImageUrl { get; set; }

    /// <summary>What this group is, in the community's own words (Chapter, Committee, Team…).</summary>
    [MaxLength(40)]
    public string? Kind { get; set; }
    /// <summary>Show this group on the public landing page.</summary>
    public bool IsPublic { get; set; }
}

public class UpdateCommunityRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(ReservEase.Alumni.Common.Sdk.Text.RichText.MaxHtmlLength)]
    public string? Description { get; set; }

    public string? CoverImageUrl { get; set; }

    [MaxLength(40)]
    public string? Kind { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Show this group on the public landing page. Left out, it stays as it is (so toggling a group on or off never changes it).</summary>
    public bool? IsPublic { get; set; }
}

public class SetCommunityMemberRoleRequest
{
    /// <summary>"Member" or "Leader".</summary>
    [Required]
    public string Role { get; set; } = "Member";
}
