using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IJobService
{
    Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter, AuthData admin);
    Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId, AuthData admin);
    Task<IApiResponse<JobDto>> CreateJobAsync(CreateJobRequest request, AuthData admin);
    Task<IApiResponse<JobDto>> UpdateJobAsync(UpdateJobRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteJobAsync(string jobId, AuthData admin);
    Task<IApiResponse<object>> CloseJobAsync(string jobId, AuthData admin);
}
