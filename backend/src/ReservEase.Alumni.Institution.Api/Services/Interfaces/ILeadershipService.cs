using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface ILeadershipService
{
    /// <summary>What a role can be allowed to do. The institution ticks from this list when it makes a role.</summary>
    IApiResponse<List<PrivilegeItem>> GetPrivileges();

    /// <summary>How often leaders may message their groups. Chosen by the institution.</summary>
    Task<IApiResponse<LeadershipSettingsItem>> GetSettingsAsync();
    Task<IApiResponse<LeadershipSettingsItem>> UpdateSettingsAsync(SaveLeadershipSettingsRequest request, AuthData admin);

    Task<IApiResponse<List<LeadershipRoleItem>>> GetRolesAsync();
    Task<IApiResponse<LeadershipRoleItem>> CreateRoleAsync(SaveLeadershipRoleRequest request, AuthData admin);
    Task<IApiResponse<LeadershipRoleItem>> UpdateRoleAsync(string id, SaveLeadershipRoleRequest request, AuthData admin);
    /// <summary>A role nobody holds. One that is still held is refused, so nobody loses access without an admin choosing it.</summary>
    Task<IApiResponse<object>> DeleteRoleAsync(string id, AuthData admin);

    /// <summary>Posts for one year group or community, or every post when no scope is given. Also the posts one member holds.</summary>
    Task<IApiResponse<List<LeadershipPostItem>>> GetPostsAsync(string? scopeType, string? scopeId, string? memberId);

    Task<IApiResponse<LeadershipPostItem>> AppointAsync(AppointLeaderRequest request, AuthData admin);
    /// <summary>Gives someone a different role in the same place, keeping their appointment and its place in the history instead of removing and re-appointing.</summary>
    Task<IApiResponse<LeadershipPostItem>> ChangeRoleAsync(string id, string roleId, AuthData admin);
    Task<IApiResponse<object>> RemoveAsync(string id, AuthData admin);
}
