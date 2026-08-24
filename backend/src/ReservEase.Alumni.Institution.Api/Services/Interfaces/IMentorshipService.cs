using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IMentorshipService
{
    Task<IApiResponse<PgPagedResult<MentorProfileDto>>> GetMentorProfilesAsync(MentorProfileFilter filter, AuthData admin);
    Task<IApiResponse<object>> ApproveMentorAsync(string profileId, AuthData admin);
    Task<IApiResponse<object>> RejectMentorAsync(string profileId, AuthData admin);
    Task<IApiResponse<PgPagedResult<MentorshipRequestDto>>> GetRequestsAsync(MentorshipRequestFilter filter, AuthData admin);
}
