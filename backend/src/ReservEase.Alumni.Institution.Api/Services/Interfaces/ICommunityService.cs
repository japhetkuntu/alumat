using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface ICommunityService
{
    Task<IApiResponse<List<CommunityListItem>>> GetCommunitiesAsync();
    Task<IApiResponse<CommunityListItem>> CreateCommunityAsync(CreateCommunityRequest request, string createdBy);
    Task<IApiResponse<CommunityListItem>> UpdateCommunityAsync(string id, UpdateCommunityRequest request, string updatedBy);
    Task<IApiResponse<List<CommunityMemberItem>>> GetCommunityMembersAsync(string communityId);
    Task<IApiResponse<object>> SetMemberRoleAsync(string communityId, string memberId, SetCommunityMemberRoleRequest request, string updatedBy);
}
