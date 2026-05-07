using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IMentorshipService
{
    Task<IApiResponse<PgPagedResult<MentorProfileDto>>> GetMentorProfilesAsync(MentorProfileFilter filter, AuthData admin);
    Task<IApiResponse<object>> ApproveMentorAsync(string profileId, AuthData admin);
    Task<IApiResponse<object>> RejectMentorAsync(string profileId, AuthData admin);
    Task<IApiResponse<PgPagedResult<MentorshipRequestDto>>> GetRequestsAsync(MentorshipRequestFilter filter, AuthData admin);
}
