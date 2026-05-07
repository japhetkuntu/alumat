using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface ILeaderboardService
{
    Task<IApiResponse<List<YearGroupLeaderboardEntryDto>>> GetLeaderboardAsync();
}
