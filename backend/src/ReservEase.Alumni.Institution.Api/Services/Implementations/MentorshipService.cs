using ReservEase.Alumni.Institution.Api.Actors;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class MentorshipService(
    IAlumniPgRepository<MentorProfile> profileRepo,
    IAlumniPgRepository<MentorshipRequest> requestRepo,
    IAlumniPgRepository<Member> memberRepo,
    INotificationActor notificationActor,
    ICurrentTenantService currentTenant,
    ILogger<MentorshipService> logger) : IMentorshipService
{
    public async Task<IApiResponse<PgPagedResult<MentorProfileDto>>> GetMentorProfilesAsync(MentorProfileFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetMentorProfiles request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
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
                        )))
                  && (isSuper || (p.YearGroups != null && p.YearGroups.Any(__y => yearGroups.Contains(__y)))));

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

            // ToDto() reads name/photo from the MemberSnapshot frozen on the mentor
            // profile at creation time — refresh both from the live Member records
            // here (one batched lookup, not one query per row) so a later name/photo
            // change actually shows up.
            var mentorMemberIds = dtoResult.Results.Select(d => d.MemberId).Distinct().ToList();
            var mentorMembers = (await memberRepo.GetAllAsync(m => mentorMemberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtoResult.Results)
            {
                if (mentorMembers.TryGetValue(dto.MemberId, out var m))
                {
                    dto.MemberName = $"{m.FirstName} {m.LastName}";
                    dto.MemberProfilePictureUrl = m.ProfilePictureUrl;
                }
            }

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
            if (admin.Role == StaffRoles.ScopedAdmin)
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Scoped admins cannot manage mentorship");

            logger.LogInformation("ApproveMentor request for profileId: {ProfileId} by admin {AdminId}", profileId, admin.Id);

            var profile = await profileRepo.GetByIdAsync(profileId);
            if (profile is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");

            if (!admin.CanModifyScopedItem(profile.YearGroups, profile.CreatedBy))
            {
                logger.LogWarning("Denied mentor approval access for admin {AdminId} to mentor profile {ProfileId} (adminYear={AdminYear}, profileYears={ProfileYears}, createdBy={CreatedBy})",
                    admin.Id, profileId, profile.YearGroups ?? new List<int>(), profile.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");
            }

            profile.Status = "Approved";
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = admin.Id;
            await profileRepo.UpdateAsync(profile);

            notificationActor.Tell(new DispatchMentorProfileDecisionCommand(currentTenant.InstitutionId!, profile.MemberId, true, profile.Id));

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
            if (admin.Role == StaffRoles.ScopedAdmin)
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Scoped admins cannot manage mentorship");

            logger.LogInformation("RejectMentor request for profileId: {ProfileId} by admin {AdminId}", profileId, admin.Id);

            var profile = await profileRepo.GetByIdAsync(profileId);
            if (profile is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");

            if (!admin.CanModifyScopedItem(profile.YearGroups, profile.CreatedBy))
            {
                logger.LogWarning("Denied mentor reject access for admin {AdminId} to mentor profile {ProfileId} (adminYear={AdminYear}, profileYears={ProfileYears}, createdBy={CreatedBy})",
                    admin.Id, profileId, profile.YearGroups ?? new List<int>(), profile.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor profile not found");
            }

            profile.Status = "Rejected";
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = admin.Id;
            await profileRepo.UpdateAsync(profile);

            notificationActor.Tell(new DispatchMentorProfileDecisionCommand(currentTenant.InstitutionId!, profile.MemberId, false, profile.Id));

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
            logger.LogInformation("GetMentorshipRequests request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);

            // Requests carry no scope of their own — they inherit their mentor
            // profile's YearGroups, so a scoped admin's view is narrowed to
            // requests aimed at a mentor in their own batch.
            List<string>? scopedProfileIds = null;
            if (admin.Role == StaffRoles.ScopedAdmin)
            {
                var yearGroups = admin.YearGroups ?? new List<int>();
                scopedProfileIds = (await profileRepo.GetAllAsync(p => p.YearGroups != null && p.YearGroups.Any(y => yearGroups.Contains(y))))
                    .Select(p => p.Id)
                    .ToList();
            }

            var result = await requestRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => (string.IsNullOrEmpty(filter.Status) || r.Status == filter.Status)
                  && (scopedProfileIds == null || scopedProfileIds.Contains(r.MentorProfileId)));

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

            // ToDto() reads the mentor's and mentee's names/photo from the snapshots
            // frozen on the request at creation time — refresh both from the live
            // Member records here (batched lookups, not one query per row) so a later
            // name/photo change actually shows up.
            var mentorProfileIds = dtoResult.Results.Select(d => d.MentorProfileId).Distinct().ToList();
            var mentorProfiles = (await profileRepo.GetAllAsync(p => mentorProfileIds.Contains(p.Id))).ToDictionary(p => p.Id);
            var menteeIds = dtoResult.Results.Select(d => d.MenteeId).Distinct().ToList();
            var involvedMemberIds = mentorProfiles.Values.Select(p => p.MemberId).Concat(menteeIds).Distinct().ToList();
            var involvedMembers = (await memberRepo.GetAllAsync(m => involvedMemberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtoResult.Results)
            {
                if (mentorProfiles.TryGetValue(dto.MentorProfileId, out var mp) && involvedMembers.TryGetValue(mp.MemberId, out var mentorMember))
                    dto.MentorProfileName = $"{mentorMember.FirstName} {mentorMember.LastName}";
                if (involvedMembers.TryGetValue(dto.MenteeId, out var mentee))
                {
                    dto.MenteeName = $"{mentee.FirstName} {mentee.LastName}";
                    dto.MenteeProfilePictureUrl = mentee.ProfilePictureUrl;
                }
            }

            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving mentorship requests — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<MentorshipRequestDto>>("Failed to retrieve mentorship requests");
        }
    }
}
