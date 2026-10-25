using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface ICommunityService
{
    Task<IApiResponse<List<CommunityListItem>>> GetCommunitiesAsync();
    Task<IApiResponse<CommunityGrowthSummary>> GetGrowthAsync();
    Task<IApiResponse<CommunityListItem>> CreateCommunityAsync(CreateCommunityRequest request, string createdBy);
    Task<IApiResponse<CommunityListItem>> UpdateCommunityAsync(string id, UpdateCommunityRequest request, string updatedBy);
    Task<IApiResponse<CommunityListItem>> SubmitPayoutSetupAsync(string id, SubmitBatchPayoutSetupRequest request, AuthData actor);
    Task<IApiResponse<List<CommunityMemberItem>>> GetCommunityMembersAsync(string communityId);
    /// <summary>Approves or declines a member's request to join. The member is told, with the reason if one is given.</summary>
    Task<IApiResponse<object>> DecideJoinRequestAsync(string communityId, string memberId, bool approve, string? reason, AuthData admin);

    /// <summary>Takes an approved member (a leader too) out of the group. They are told, and may ask to join again.</summary>
    Task<IApiResponse<object>> RemoveMemberAsync(string communityId, string memberId, string? reason, AuthData admin);

    Task<IApiResponse<object>> SetMemberRoleAsync(string communityId, string memberId, SetCommunityMemberRoleRequest request, string updatedBy);
}
