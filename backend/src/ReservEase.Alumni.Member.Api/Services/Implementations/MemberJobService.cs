using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class MemberJobService(
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    ILogger<MemberJobService> logger) : IMemberJobService
{
    private async Task<bool> IsApprovedCommunityMemberAsync(string communityId, string memberId)
    {
        var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
        return membership is not null && membership.Status == "Approved";
    }

    public async Task<IApiResponse<PgPagedResult<JobDto>>> GetJobsAsync(JobFilter filter, string memberId)
    {
        try
        {
            logger.LogInformation("GetJobs request — filter: {Filter}", filter.Serialize());

            if (!string.IsNullOrEmpty(filter.CommunityId) && !await IsApprovedCommunityMemberAsync(filter.CommunityId, memberId))
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<JobDto>>("You must be an approved member of this community to view its job postings");

            var member = await memberRepo.GetByIdAsync(memberId);
            var memberYear = member?.GraduationYear;

            var result = await jobRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                j => j.Status == "Active"
                  && (string.IsNullOrEmpty(filter.CommunityId) ? j.CommunityId == null : j.CommunityId == filter.CommunityId)
                  && (j.YearGroups == null || j.YearGroups.Count == 0 || (memberYear.HasValue && j.YearGroups.Contains(memberYear.Value)))
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

    public async Task<IApiResponse<JobDto>> GetJobByIdAsync(string jobId, string memberId)
    {
        try
        {
            logger.LogInformation("GetJobById request — jobId: {JobId}", jobId);
            var job = await jobRepo.GetByIdAsync(jobId);
            if (job is null || job.Status != "Active")
                return ApiResponseExtensions.ToNotFoundApiResponse<JobDto>("Job not found");

            if (job.YearGroups is { Count: > 0 })
            {
                var member = await memberRepo.GetByIdAsync(memberId);
                if (member is null || !job.YearGroups.Contains(member.GraduationYear))
                    return ApiResponseExtensions.ToNotFoundApiResponse<JobDto>("Job not found");
            }

            return job.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving job {JobId}", jobId);
            return ApiResponseExtensions.ToServerErrorApiResponse<JobDto>("Failed to retrieve job");
        }
    }
}
