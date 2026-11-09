using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

/// <summary>
/// News, jobs and shared links a leader makes for their own group. Each call needs the matching privilege in that exact group, shows the
/// group's items, and lets the leader change only the ones they made. Everything is aimed at the group alone and is never public.
/// </summary>
public interface IManageContentService
{
    Task<IApiResponse<List<GroupNewsDto>>> GetNewsAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<GroupNewsDto>> CreateNewsAsync(string memberId, string scopeType, string scopeId, SaveGroupNewsRequest request);
    Task<IApiResponse<GroupNewsDto>> UpdateNewsAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupNewsRequest request);
    Task<IApiResponse<object>> DeleteNewsAsync(string memberId, string scopeType, string scopeId, string id);

    Task<IApiResponse<List<GroupJobDto>>> GetJobsAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<GroupJobDto>> CreateJobAsync(string memberId, string scopeType, string scopeId, SaveGroupJobRequest request);
    Task<IApiResponse<GroupJobDto>> UpdateJobAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupJobRequest request);
    Task<IApiResponse<object>> CloseJobAsync(string memberId, string scopeType, string scopeId, string id);

    Task<IApiResponse<List<GroupResourceDto>>> GetResourcesAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<GroupResourceDto>> CreateResourceAsync(string memberId, string scopeType, string scopeId, SaveGroupResourceRequest request);
    Task<IApiResponse<GroupResourceDto>> UpdateResourceAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupResourceRequest request);
    Task<IApiResponse<object>> DeleteResourceAsync(string memberId, string scopeType, string scopeId, string id);
}
