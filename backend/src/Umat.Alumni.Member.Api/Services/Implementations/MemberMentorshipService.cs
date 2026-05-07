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

public class MemberMentorshipService(
    IAlumniPgRepository<MentorProfile> profileRepo,
    IAlumniPgRepository<MentorshipRequest> requestRepo,
    ILogger<MemberMentorshipService> logger) : IMemberMentorshipService
{
    public async Task<IApiResponse<PgPagedResult<MentorProfileDto>>> GetMentorsAsync(BaseFilter filter)
    {
        try
        {
            logger.LogInformation("GetMentors request — filter: {Filter}", filter.Serialize());
            var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            var result = await profileRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                p => p.Status == "Approved" && p.CurrentMenteeCount < p.MaxMentees
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
            logger.LogError(e, "Error retrieving mentors");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<MentorProfileDto>>("Failed to retrieve mentors");
        }
    }

    public async Task<IApiResponse<object>> RegisterAsMentorAsync(RegisterAsMentorRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("RegisterAsMentor request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            var existing = await profileRepo.GetOneAsync(p => p.MemberId == member.Id);
            if (existing is not null)
            {
                if (existing.Status == "Pending")
                    return ApiResponseExtensions.ToConflictApiResponse<object>("You already have a pending mentor profile");

                if (existing.Status == "Approved")
                    return ApiResponseExtensions.ToConflictApiResponse<object>("You already have an approved mentor profile");

                if (existing.Status == "Rejected")
                {
                    existing.Area = request.Area;
                    existing.Bio = request.Bio;
                    existing.MaxMentees = request.MaxMentees;
                    existing.Status = "Pending";
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = member.Id;
                    existing.Member = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl };

                    await profileRepo.UpdateAsync(existing);

                    logger.LogInformation("Rejected mentor profile {ProfileId} resubmitted for member {MemberId}", existing.Id, member.Id);
                    return new object().ToCreatedApiResponse("Mentor profile resubmitted for review");
                }

                return ApiResponseExtensions.ToConflictApiResponse<object>("You already have a mentor profile");
            }

            var profile = new MentorProfile
            {
                MemberId = member.Id,
                Member = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl },
                Area = request.Area,
                Bio = request.Bio,
                MaxMentees = request.MaxMentees,
                Status = "Pending",
                CreatedBy = member.Id,
            };
            await profileRepo.AddAsync(profile);

            logger.LogInformation("Mentor profile {ProfileId} created for member {MemberId}", profile.Id, member.Id);
            return new object().ToCreatedApiResponse("Mentor profile submitted for review");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error registering as mentor for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to register as mentor");
        }
    }

    public async Task<IApiResponse<object>> RequestMentorshipAsync(RequestMentorshipRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("RequestMentorship request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            var mentor = await profileRepo.GetByIdAsync(request.MentorProfileId);
            if (mentor is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Mentor not found");
            if (mentor.Status != "Approved")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Mentor is not available");
            if (mentor.MemberId == member.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You cannot request mentorship from your own profile");

            var existing = await requestRepo.GetOneAsync(
                r => r.MentorProfileId == request.MentorProfileId
                  && r.MenteeId == member.Id
                  && r.Status == "Pending");
            if (existing is not null)
                return ApiResponseExtensions.ToConflictApiResponse<object>("You already have a pending request to this mentor");

            var mentorshipReq = new MentorshipRequest
            {
                MentorProfileId = request.MentorProfileId,
                MentorProfile = new MentorProfileSnapshot
                {
                    Id = mentor.Id,
                    MemberId = mentor.MemberId,
                    Member = mentor.Member,
                    Area = mentor.Area,
                    Bio = mentor.Bio,
                    MaxMentees = mentor.MaxMentees,
                    CurrentMenteeCount = mentor.CurrentMenteeCount,
                    Status = mentor.Status,
                },
                MenteeId = member.Id,
                Mentee = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl },
                Area = request.Area,
                Message = request.Message,
                Status = "Pending",
                CreatedBy = member.Id,
            };
            await requestRepo.AddAsync(mentorshipReq);

            logger.LogInformation("Mentorship request {RequestId} sent by member {MemberId} to mentor {MentorProfileId}", mentorshipReq.Id, member.Id, request.MentorProfileId);
            return new object().ToCreatedApiResponse("Mentorship request sent");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error requesting mentorship from {MentorProfileId} by member {MemberId}", request.MentorProfileId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to send mentorship request");
        }
    }

    public async Task<IApiResponse<PgPagedResult<MentorshipRequestDto>>> GetMyRequestsAsync(
        string memberId, BaseFilter filter)
    {
        try
        {
            logger.LogInformation("GetMyMentorshipRequests for member {MemberId} — filter: {Filter}", memberId, filter.Serialize());
            var result = await requestRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => r.MenteeId == memberId);

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
            logger.LogError(e, "Error retrieving mentorship requests for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<MentorshipRequestDto>>("Failed to retrieve mentorship requests");
        }
    }

    public async Task<IApiResponse<MentorProfileDto>> GetMyMentorProfileAsync(string memberId)
    {
        try
        {
            logger.LogInformation("GetMyMentorProfile for member {MemberId}", memberId);
            var profile = await profileRepo.GetOneAsync(p => p.MemberId == memberId);
            if (profile is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<MentorProfileDto>("You don't have a mentor profile");
            return profile.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving mentor profile for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<MentorProfileDto>("Failed to retrieve mentor profile");
        }
    }

    public async Task<IApiResponse<PgPagedResult<MentorshipRequestDto>>> GetIncomingRequestsAsync(
        string memberId, BaseFilter filter)
    {
        try
        {
            logger.LogInformation("GetIncomingRequests for mentor member {MemberId} — filter: {Filter}", memberId, filter.Serialize());
            var profile = await profileRepo.GetOneAsync(p => p.MemberId == memberId);
            if (profile is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PgPagedResult<MentorshipRequestDto>>("You don't have a mentor profile");

            var result = await requestRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => r.MentorProfileId == profile.Id);

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
            logger.LogError(e, "Error retrieving incoming requests for mentor member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<MentorshipRequestDto>>("Failed to retrieve incoming requests");
        }
    }

    public async Task<IApiResponse<object>> AcceptRequestAsync(string requestId, AuthData mentor)
    {
        try
        {
            logger.LogInformation("AcceptRequest {RequestId} by mentor {MentorId}", requestId, mentor.Id);

            var request = await requestRepo.GetByIdAsync(requestId);
            if (request is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Request not found");

            var profile = await profileRepo.GetOneAsync(p => p.MemberId == mentor.Id);
            if (profile is null || request.MentorProfileId != profile.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You are not the mentor for this request");

            if (request.Status != "Pending")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Request is already {request.Status}");

            if (profile.CurrentMenteeCount >= profile.MaxMentees)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You have reached your maximum mentee limit");

            request.Status = "Accepted";
            request.UpdatedAt = DateTime.UtcNow;
            request.UpdatedBy = mentor.Id;
            await requestRepo.UpdateAsync(request);

            profile.CurrentMenteeCount += 1;
            profile.UpdatedAt = DateTime.UtcNow;
            await profileRepo.UpdateAsync(profile);

            logger.LogInformation("Request {RequestId} accepted by mentor {MentorId}", requestId, mentor.Id);
            return new object().ToOkApiResponse("Mentorship request accepted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error accepting request {RequestId} by mentor {MentorId}", requestId, mentor.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to accept request");
        }
    }

    public async Task<IApiResponse<object>> RejectRequestAsync(string requestId, AuthData mentor)
    {
        try
        {
            logger.LogInformation("RejectRequest {RequestId} by mentor {MentorId}", requestId, mentor.Id);

            var request = await requestRepo.GetByIdAsync(requestId);
            if (request is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Request not found");

            var profile = await profileRepo.GetOneAsync(p => p.MemberId == mentor.Id);
            if (profile is null || request.MentorProfileId != profile.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You are not the mentor for this request");

            if (request.Status != "Pending")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Request is already {request.Status}");

            request.Status = "Rejected";
            request.UpdatedAt = DateTime.UtcNow;
            request.UpdatedBy = mentor.Id;
            await requestRepo.UpdateAsync(request);

            logger.LogInformation("Request {RequestId} rejected by mentor {MentorId}", requestId, mentor.Id);
            return new object().ToOkApiResponse("Mentorship request rejected");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting request {RequestId} by mentor {MentorId}", requestId, mentor.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reject request");
        }
    }
}
