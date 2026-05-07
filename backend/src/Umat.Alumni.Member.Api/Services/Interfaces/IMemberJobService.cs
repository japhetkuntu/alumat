using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IMemberJobService
{
    Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter);
    Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId);
}
