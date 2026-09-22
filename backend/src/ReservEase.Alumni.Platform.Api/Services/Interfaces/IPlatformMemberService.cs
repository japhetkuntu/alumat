using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlatformMemberService
{
    Task<IApiResponse<PgPagedResult<PlatformMemberListItem>>> GetMembersAsync(PlatformMemberFilter filter);
    Task<IApiResponse<object>> UpdateMemberProfileAsync(string id, UpdatePlatformMemberProfileRequest request, string actorId, string actorName);
}
