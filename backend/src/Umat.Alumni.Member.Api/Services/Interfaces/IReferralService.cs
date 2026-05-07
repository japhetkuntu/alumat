using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IReferralService
{
    Task<IApiResponse<object>> GetMyReferralInfoAsync(AuthData member);
    Task<IApiResponse<object>> InviteAsync(string email, AuthData member);
    Task<IApiResponse<List<ReferralDto>>> GetMyReferralsAsync(string memberId);
}
