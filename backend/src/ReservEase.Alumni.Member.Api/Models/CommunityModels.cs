namespace ReservEase.Alumni.Member.Api.Models;

public record CommunityDto(
    string Id, string Name, string? Description, string? CoverImageUrl,
    int MemberCount, string? MyStatus, string? MyRole,
    // What the administrators call this group (Chapter, Committee…); null reads as a plain group.
    string? Kind = null);

/// <summary>What a community leader needs to bring their existing group in: their own invite code and how it is going.</summary>
public record CommunityInviteInfoDto(
    string CommunityId, string CommunityName, string ReferralCode,
    int ApprovedMembers, int PendingRequests, int JoinedViaInvites, int JoinedLast30Days);

public record CommunityMemberDto(string MemberId, string Name, string? ProfilePictureUrl, string Role);

public record JoinRequestDto(string MembershipId, string MemberId, string MemberName, string? ProfilePictureUrl, DateTime RequestedAt);
