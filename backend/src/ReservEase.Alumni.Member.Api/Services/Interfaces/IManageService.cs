using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

/// <summary>
/// What a member does as a leader of a year group or community. Every call asks the one leadership access check whether the member holds a
/// role with the matching privilege in that exact group, so a leader can never act on a group they were not appointed to.
/// </summary>
public interface IManageService
{
    Task<IApiResponse<List<ManageGroupDto>>> GetGroupsAsync(string memberId);
    Task<IApiResponse<GroupSummaryDto>> GetSummaryAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<List<GroupMemberDto>>> GetMembersAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<object>> DecideAsync(string memberId, string scopeType, string scopeId, string targetMemberId, bool approve);

    Task<IApiResponse<List<GroupMessageHistoryDto>>> GetMessagesAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<ManageChannelsDto>> GetChannelsAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<GroupMessageResult>> MessageAsync(string memberId, string scopeType, string scopeId, string text, IReadOnlyCollection<string>? channels = null);

    Task<IApiResponse<List<GroupEventDto>>> GetEventsAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<GroupEventDto>> CreateEventAsync(string memberId, string scopeType, string scopeId, SaveGroupEventRequest request);
    Task<IApiResponse<GroupEventDto>> UpdateEventAsync(string memberId, string scopeType, string scopeId, string eventId, SaveGroupEventRequest request);
    Task<IApiResponse<object>> CancelEventAsync(string memberId, string scopeType, string scopeId, string eventId);
}
