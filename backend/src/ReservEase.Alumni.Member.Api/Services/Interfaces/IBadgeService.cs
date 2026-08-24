using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IBadgeService
{
    Task<IApiResponse<List<MemberBadgeDto>>> GetMyBadgesAsync(string memberId);
    Task EvaluateAndAwardBadgesAsync(string memberId);
}
