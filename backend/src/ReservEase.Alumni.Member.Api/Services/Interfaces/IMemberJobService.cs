using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberJobService
{
    Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter, string memberId);
    Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId, string memberId);
    /// <summary>A member suggesting an opportunity. It is held for an administrator to review and is not visible to anyone else until approved.</summary>
    Task<IApiResponse<JobDto>> SuggestAsync(string memberId, SuggestOpportunityRequest request);
}
