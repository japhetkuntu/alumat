namespace ReservEase.Alumni.Member.Api.Models;

public record CommunityDto(
    string Id, string Name, string? Description, string? CoverImageUrl,
    int MemberCount, string? MyStatus, string? MyRole);

public record CommunityMemberDto(string MemberId, string Name, string? ProfilePictureUrl, string Role);

public record JoinRequestDto(string MembershipId, string MemberId, string MemberName, string? ProfilePictureUrl, DateTime RequestedAt);
