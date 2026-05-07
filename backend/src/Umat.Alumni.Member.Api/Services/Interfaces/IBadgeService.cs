using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IBadgeService
{
    Task<IApiResponse<List<MemberBadgeDto>>> GetMyBadgesAsync(string memberId);
    Task EvaluateAndAwardBadgesAsync(string memberId);
}
