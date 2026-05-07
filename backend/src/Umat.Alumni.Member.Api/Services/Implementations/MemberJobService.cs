using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Extensions;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class MemberJobService(
    IAlumniPgRepository<Job> jobRepo,
    ILogger<MemberJobService> logger) : IMemberJobService
{
    public async Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter)
    {
        try
        {
            logger.LogInformation("GetJobs request — filter: {Filter}", filter.Serialize());
            var result = await jobRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                j => j.Status == "Active"
                  && (string.IsNullOrEmpty(filter.Type) || j.Type == filter.Type)
                  && (string.IsNullOrEmpty(filter.Location) || j.Location.Contains(filter.Location))
                  && (!filter.PostedAfter.HasValue || j.CreatedAt >= filter.PostedAfter.Value)
                  && (!filter.PostedBefore.HasValue || j.CreatedAt <= filter.PostedBefore.Value)
                  && (string.IsNullOrEmpty(filter.Search)
                      || j.Title.Contains(filter.Search)
                      || j.Company.Contains(filter.Search)
                      || j.Location.Contains(filter.Search)));
            var dtoResult = new PgPagedResult<JobDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(j => j.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving jobs — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<JobDto>>("Failed to retrieve jobs");
        }
    }

    public async Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId)
    {
        try
        {
            logger.LogInformation("GetJobById request — jobId: {JobId}", jobId);
            var job = await jobRepo.GetByIdAsync(jobId);
            if (job is null || job.Status != "Active")
                return ApiResponseExtensions.ToNotFoundApiResponse<JobDto>("Job not found");
            return job.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving job {JobId}", jobId);
            return ApiResponseExtensions.ToServerErrorApiResponse<JobDto>("Failed to retrieve job");
        }
    }
}
