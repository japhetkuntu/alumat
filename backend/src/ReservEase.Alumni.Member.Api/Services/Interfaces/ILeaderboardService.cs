using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface ILeaderboardService
{
    Task<IApiResponse<List<YearGroupLeaderboardEntryDto>>> GetLeaderboardAsync();
}
