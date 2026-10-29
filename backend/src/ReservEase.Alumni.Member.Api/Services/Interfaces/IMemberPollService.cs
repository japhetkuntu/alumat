using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberPollService
{
    Task<IApiResponse<List<MemberPollListItem>>> GetPollsAsync(AuthData member);
    Task<IApiResponse<MemberPollDetail>> GetPollAsync(string id, AuthData member);
    Task<IApiResponse<object>> CastBallotAsync(string id, CastBallotRequest request, AuthData member);
    Task<IApiResponse<MemberPollResults>> GetResultsAsync(string id, AuthData member);
}
