using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IReferralService
{
    Task<IApiResponse<ReferralInfoDto>> GetMyReferralInfoAsync(AuthData member);
    Task<IApiResponse<object>> InviteAsync(string email, AuthData member);
    Task<IApiResponse<List<ReferralDto>>> GetMyReferralsAsync(string memberId);
    Task<IApiResponse<List<ReferralLeaderboardEntryDto>>> GetLeaderboardAsync();
}
