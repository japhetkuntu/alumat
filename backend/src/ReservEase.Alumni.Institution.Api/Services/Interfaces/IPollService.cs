using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IPollService
{
    Task<IApiResponse<List<PollListItem>>> GetPollsAsync(bool archived);
    Task<IApiResponse<PollDetail>> GetPollAsync(string id);
    Task<IApiResponse<PollDetail>> CreatePollAsync(SavePollRequest request, AuthData admin);
    Task<IApiResponse<PollDetail>> UpdatePollAsync(string id, SavePollRequest request, AuthData admin);
    Task<IApiResponse<PollDetail>> PublishAsync(string id, AuthData admin);
    Task<IApiResponse<PollDetail>> UnpublishAsync(string id, AuthData admin);
    Task<IApiResponse<object>> DeleteAsync(string id, AuthData admin);
    Task<IApiResponse<PollDetail>> SetArchivedAsync(string id, bool archived, AuthData admin);
    Task<IApiResponse<PollResultsDto>> GetResultsAsync(string id);
    Task<IApiResponse<PollResultsDto>> PublishResultsAsync(string id, AuthData admin);
}
