using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IJobService
{
    Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter, AuthData admin);
    Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId, AuthData admin);
    Task<IApiResponse<JobDto>> CreateJobAsync(CreateJobRequest request, AuthData admin);
    Task<IApiResponse<JobDto>> UpdateJobAsync(UpdateJobRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteJobAsync(string jobId, AuthData admin);
    Task<IApiResponse<object>> CloseJobAsync(string jobId, AuthData admin);
}
