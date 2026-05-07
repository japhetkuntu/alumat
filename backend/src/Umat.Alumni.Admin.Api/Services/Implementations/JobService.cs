using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Storage.Sdk.Services;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class JobService(
    IAlumniPgRepository<Job> jobRepo,
    IStorageService storageService,
    ILogger<JobService> logger) : IJobService
{
    public async Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetJobs request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;

            var result = await jobRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                j => (string.IsNullOrEmpty(filter.Status) || j.Status == filter.Status)
                  && (string.IsNullOrEmpty(filter.Type) || j.Type == filter.Type)
                  && (string.IsNullOrEmpty(filter.Location) || j.Location.Contains(filter.Location))
                  && (!filter.PostedAfter.HasValue || j.CreatedAt >= filter.PostedAfter.Value)
                  && (!filter.PostedBefore.HasValue || j.CreatedAt <= filter.PostedBefore.Value)
                  && (string.IsNullOrEmpty(filter.Search)
                      || j.Title.Contains(filter.Search)
                      || j.Company.Contains(filter.Search)
                      || j.Location.Contains(filter.Search))
                  && (isSuper || (yearGroup.HasValue && j.YearGroups != null && j.YearGroups.Contains(yearGroup.Value))));
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

    public async Task<IApiResponse<JobDto>> CreateJobAsync(CreateJobRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreateJob request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;

            var job = new Job
            {
                Title = request.Title,
                Company = request.Company,
                Location = request.Location,
                Description = request.Description,
                Type = request.Type,
                ApplyUrl = request.ApplyUrl,
                Deadline = request.Deadline.HasValue ? DateTime.SpecifyKind(request.Deadline.Value, DateTimeKind.Utc) : (DateTime?)null,
                Status = "Active",
                PostedBy = admin.Id,
                CreatedBy = admin.Id,
                YearGroups = isSuper ? request.YearGroups : (yearGroup.HasValue ? new List<int> { yearGroup.Value } : null),
            };

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                job.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            await jobRepo.AddAsync(job);
            logger.LogInformation("Job {JobId} created by admin {AdminId}", job.Id, admin.Id);
            return job.ToDto().ToCreatedApiResponse("Job posted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating job: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<JobDto>("Failed to create job");
        }
    }

    public async Task<IApiResponse<JobDto>> UpdateJobAsync(UpdateJobRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdateJob request for jobId: {JobId} by admin {AdminId}", request.JobId, admin.Id);
            var job = await jobRepo.GetByIdAsync(request.JobId);
            if (job is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<JobDto>("Job not found");

            if (!admin.CanModifyYearGroupScopedItem(job.YearGroups, job.CreatedBy))
            {
                logger.LogWarning("Denied job update access for admin {AdminId} to job {JobId} (adminYear={AdminYear}, jobYears={JobYears}, createdBy={CreatedBy})",
                    admin.Id, job.Id, admin.GraduationYear, job.YearGroups ?? new List<int>(), job.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<JobDto>("Job not found");
            }

            job.Title = request.Title;
            job.Company = request.Company;
            job.Location = request.Location;
            job.Type = request.Type;
            job.Description = request.Description;
            job.ApplyUrl = request.ApplyUrl;
            job.Deadline = request.Deadline.HasValue ? DateTime.SpecifyKind(request.Deadline.Value, DateTimeKind.Utc) : (DateTime?)null;
            job.Status = request.Status;
            job.YearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                job.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            job.UpdatedAt = DateTime.UtcNow;
            job.UpdatedBy = admin.Id;
            await jobRepo.UpdateAsync(job);
            logger.LogInformation("Job {JobId} updated by admin {AdminId}", job.Id, admin.Id);
            return job.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating job {JobId} by admin {AdminId}", request.JobId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<JobDto>("Failed to update job");
        }
    }

    public async Task<IApiResponse<object>> DeleteJobAsync(string jobId, AuthData admin)
    {
        try
        {
            logger.LogInformation("DeleteJob request for jobId: {JobId} (admin: {AdminId})", jobId, admin.Id);
            var job = await jobRepo.GetByIdAsync(jobId);
            if (job is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Job not found");

            if (!admin.CanModifyYearGroupScopedItem(job.YearGroups, job.CreatedBy))
            {
                logger.LogWarning("Denied job delete access for admin {AdminId} to job {JobId} (adminYear={AdminYear}, jobYears={JobYears}, createdBy={CreatedBy})",
                    admin.Id, jobId, admin.GraduationYear, job.YearGroups ?? new List<int>(), job.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Job not found");
            }

            await jobRepo.RemoveAsync(job);
            logger.LogInformation("Job {JobId} deleted", jobId);
            return new object().ToOkApiResponse("Job deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting job {JobId}", jobId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete job");
        }
    }

    public async Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetJobById request — jobId: {JobId} (admin: {AdminId})", jobId, admin.Id);
            var job = await jobRepo.GetByIdAsync(jobId);
            if (job is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<JobDto>("Job not found");

            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;
            if (!isSuper)
            {
                if (!yearGroup.HasValue || job.YearGroups == null || !job.YearGroups.Contains(yearGroup.Value))
                {
                    logger.LogWarning("Denied job view access for admin {AdminId} to job {JobId} (adminYear={AdminYear}, jobYears={JobYears})",
                        admin.Id, jobId, admin.GraduationYear, job.YearGroups ?? new List<int>());
                    return ApiResponseExtensions.ToNotFoundApiResponse<JobDto>("Job not found");
                }
            }

            return job.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving job {JobId}", jobId);
            return ApiResponseExtensions.ToServerErrorApiResponse<JobDto>("Failed to retrieve job");
        }
    }

    public async Task<IApiResponse<object>> CloseJobAsync(string jobId, AuthData admin)
    {
        try
        {
            logger.LogInformation("CloseJob request for jobId: {JobId} (admin: {AdminId})", jobId, admin.Id);
            var job = await jobRepo.GetByIdAsync(jobId);
            if (job is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Job not found");

            if (!admin.CanModifyYearGroupScopedItem(job.YearGroups, job.CreatedBy))
            {
                logger.LogWarning("Denied job close access for admin {AdminId} to job {JobId} (adminYear={AdminYear}, jobYears={JobYears}, createdBy={CreatedBy})",
                    admin.Id, jobId, admin.GraduationYear, job.YearGroups ?? new List<int>(), job.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Job not found");
            }

            job.Status = "Closed";
            job.UpdatedAt = DateTime.UtcNow;
            await jobRepo.UpdateAsync(job);
            logger.LogInformation("Job {JobId} closed", jobId);
            return new object().ToOkApiResponse("Job closed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error closing job {JobId}", jobId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to close job");
        }
    }
}
