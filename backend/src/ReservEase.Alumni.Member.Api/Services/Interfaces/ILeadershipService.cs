using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface ILeadershipService
{
    /// <summary>The leaders of one year group or community, or the posts held by the given members (for the directory and profiles).</summary>
    Task<IApiResponse<List<LeadershipPostDto>>> GetPostsAsync(string? scopeType, string? scopeId, IReadOnlyCollection<string>? memberIds);

    /// <summary>The roles the member holds, with what each one allows. How a leader learns what they have been given.</summary>
    Task<IApiResponse<List<MyRoleDto>>> GetMineAsync(string memberId);

    /// <summary>A leader gives up one of their own roles. Only their own post can be given up.</summary>
    Task<IApiResponse<object>> StepDownAsync(string memberId, string postId);
}
