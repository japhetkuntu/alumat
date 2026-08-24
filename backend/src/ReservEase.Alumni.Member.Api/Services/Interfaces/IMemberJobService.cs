using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberJobService
{
    Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter);
    Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId);
}
