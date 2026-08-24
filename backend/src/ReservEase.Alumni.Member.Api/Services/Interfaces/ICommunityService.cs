using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface ICommunityService
{
    Task<IApiResponse<List<CommunityDto>>> GetCommunitiesAsync(string memberId);
    Task<IApiResponse<List<CommunityDto>>> GetMyCommunitiesAsync(string memberId);
    Task<IApiResponse<CommunityDto>> GetCommunityAsync(string id, string memberId);
    Task<IApiResponse<object>> JoinAsync(string id, string memberId);
    Task<IApiResponse<object>> LeaveAsync(string id, string memberId);
    Task<IApiResponse<List<CommunityMemberDto>>> GetMembersAsync(string id, string memberId);
    Task<IApiResponse<List<JoinRequestDto>>> GetJoinRequestsAsync(string id, string memberId);
    Task<IApiResponse<object>> DecideJoinRequestAsync(string id, string membershipId, bool approve, string actingMemberId);
    Task<IApiResponse<object>> RemoveMemberAsync(string id, string targetMemberId, string actingMemberId);
}
