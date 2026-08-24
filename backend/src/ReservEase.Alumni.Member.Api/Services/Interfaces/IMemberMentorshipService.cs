using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberMentorshipService
{
    Task<IApiResponse<PgPagedResult<MentorProfileDto>>> GetMentorsAsync(BaseFilter filter);
    Task<IApiResponse<object>> RegisterAsMentorAsync(RegisterAsMentorRequest request, AuthData member);
    Task<IApiResponse<object>> RequestMentorshipAsync(RequestMentorshipRequest request, AuthData member);
    Task<IApiResponse<PgPagedResult<MentorshipRequestDto>>> GetMyRequestsAsync(string memberId, BaseFilter filter);
    Task<IApiResponse<MentorProfileDto>> GetMyMentorProfileAsync(string memberId);
    Task<IApiResponse<PgPagedResult<MentorshipRequestDto>>> GetIncomingRequestsAsync(string memberId, BaseFilter filter);
    Task<IApiResponse<object>> AcceptRequestAsync(string requestId, AuthData mentor);
    Task<IApiResponse<object>> RejectRequestAsync(string requestId, AuthData mentor);
}
