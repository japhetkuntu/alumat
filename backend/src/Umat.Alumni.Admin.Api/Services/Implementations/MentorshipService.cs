using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class MentorshipService(
    IAlumniPgRepository<MentorProfile> profileRepo,
    IAlumniPgRepository<MentorshipRequest> requestRepo,
    ILogger<MentorshipService> logger) : IMentorshipService
{
    public async Task<IApiResponse<PgPagedResult<MentorProfileDto>>> GetMentorProfilesAsync(MentorProfileFilter filter, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<MentorProfileDto>>("Only super admins can manage mentorship");

            logger.LogInformation("GetMentorProfiles request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            var result = await profileRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                p => (string.IsNullOrEmpty(filter.Status) || p.Status == filter.Status)
                  && (search == null
                      || p.Area.ToLower().Contains(search.ToLower())
                      || (p.Bio != null && p.Bio.ToLower().Contains(search.ToLower()))
                      || (p.Member != null && (
                            p.Member.FirstName.ToLower().Contains(search.ToLower())
                            || p.Member.LastName.ToLower().Contains(search.ToLower())
                        ))));

            var dtoResult = new PgPagedResult<MentorProfileDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(p => p.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving mentor profiles — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<MentorProfileDto>>("Failed to retrieve mentor profiles");
        }
    }

    public async Task<IApiResponse<object>> ApproveMentorAsync(string profileId, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only super admins can manage mentorship");

            logger.LogInformation("ApproveMentor request for profileId: {ProfileId} by admin {AdminId}", profileId, admin.Id);

            var profile = await profileRepo.GetByIdAsync(profileId);
            if (profile is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");

            if (!admin.CanModifyYearGroupScopedItem(profile.YearGroups, profile.CreatedBy))
            {
                logger.LogWarning("Denied mentor approval access for admin {AdminId} to mentor profile {ProfileId} (adminYear={AdminYear}, profileYears={ProfileYears}, createdBy={CreatedBy})",
                    admin.Id, profileId, admin.GraduationYear, profile.YearGroups ?? new List<int>(), profile.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");
            }

            profile.Status = "Approved";
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = admin.Id;
            await profileRepo.UpdateAsync(profile);

            logger.LogInformation("Mentor profile {ProfileId} approved by admin {AdminId}", profileId, admin.Id);
            return new object().ToOkApiResponse("Mentor approved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error approving mentor profile {ProfileId} by admin {AdminId}", profileId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to approve mentor");
        }
    }

    public async Task<IApiResponse<object>> RejectMentorAsync(string profileId, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only super admins can manage mentorship");

            logger.LogInformation("RejectMentor request for profileId: {ProfileId} by admin {AdminId}", profileId, admin.Id);

            var profile = await profileRepo.GetByIdAsync(profileId);
            if (profile is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");

            if (!admin.CanModifyYearGroupScopedItem(profile.YearGroups, profile.CreatedBy))
            {
                logger.LogWarning("Denied mentor reject access for admin {AdminId} to mentor profile {ProfileId} (adminYear={AdminYear}, profileYears={ProfileYears}, createdBy={CreatedBy})",
                    admin.Id, profileId, admin.GraduationYear, profile.YearGroups ?? new List<int>(), profile.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");
            }

            profile.Status = "Rejected";
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = admin.Id;
            await profileRepo.UpdateAsync(profile);

            logger.LogInformation("Mentor profile {ProfileId} rejected by admin {AdminId}", profileId, admin.Id);
            return new object().ToOkApiResponse("Mentor rejected");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting mentor profile {ProfileId} by admin {AdminId}", profileId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reject mentor");
        }
    }

    public async Task<IApiResponse<PgPagedResult<MentorshipRequestDto>>> GetRequestsAsync(MentorshipRequestFilter filter, AuthData admin)
    {
        try
        {
            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<MentorshipRequestDto>>("Only super admins can manage mentorship");

            logger.LogInformation("GetMentorshipRequests request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var result = await requestRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => string.IsNullOrEmpty(filter.Status) || r.Status == filter.Status);

            var dtoResult = new PgPagedResult<MentorshipRequestDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(r => r.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving mentorship requests — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<MentorshipRequestDto>>("Failed to retrieve mentorship requests");
        }
    }
}
