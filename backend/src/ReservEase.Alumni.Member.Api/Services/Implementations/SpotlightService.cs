using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class SpotlightService(
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ICurrentTenantService currentTenant,
    ILogger<SpotlightService> logger) : ISpotlightService
{
    public async Task<IApiResponse<PgPagedResult<SpotlightDto>>> GetApprovedSpotlightsAsync(int page, int pageSize)
    {
        try
        {
            var result = await spotlightRepo.GetPagedAsync(
                page, pageSize, "FeaturedMonth", "desc",
                s => s.Status == "Approved");

            var dtos = result.Results.Select(s => s.ToDto()).ToList();

            // ToDto() reads name/photo from the MemberSnapshot frozen on the
            // Spotlight at creation time — refresh both from the live Member
            // record here (one batched lookup, not one query per row) so a
            // member's later name/photo change actually shows up. Falls back
            // to the snapshot if the member has since been deleted.
            var memberIds = dtos.Select(d => d.MemberId).Distinct().ToList();
            var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtos)
            {
                if (members.TryGetValue(dto.MemberId, out var m))
                {
                    dto.MemberGraduationYear = m.GraduationYear;
                    dto.MemberName = $"{m.FirstName} {m.LastName}";
                    dto.MemberProfilePictureUrl = m.ProfilePictureUrl;
                }
            }

            return new PgPagedResult<SpotlightDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = dtos,
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving spotlights");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<SpotlightDto>>("Failed to retrieve spotlights");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> GetSpotlightByIdAsync(string spotlightId)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            var dto = spotlight.ToDto();
            var member = await memberRepo.GetByIdAsync(spotlight.MemberId);
            if (member is not null)
            {
                dto.MemberGraduationYear = member.GraduationYear;
                dto.MemberName = $"{member.FirstName} {member.LastName}";
                dto.MemberProfilePictureUrl = member.ProfilePictureUrl;
            }

            return dto.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to retrieve spotlight");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> SubmitSpotlightAsync(SubmitSpotlightRequest request, AuthData member)
    {
        try
        {
            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            if (memberEntity is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<SpotlightDto>("Member not found.");

            // Compute dues-paid dynamically: paid all campaigns from grad year through current year
            var currentYear = DateTime.UtcNow.Year;
            var requiredCampaigns = await campaignRepo.GetAllAsync(c =>
                c.IsMembershipCampaign && c.MembershipYear.HasValue
                && c.MembershipYear.Value >= memberEntity.GraduationYear
                && c.MembershipYear.Value <= currentYear);
            bool duesRequirementMet;
            if (requiredCampaigns.Any())
            {
                var requiredIds = requiredCampaigns.Select(c => c.Id).ToHashSet();
                var confirmed = await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && c.Status == "Successful");
                var paidCount = confirmed.Count(c => requiredIds.Contains(c.CampaignId));
                duesRequirementMet = paidCount >= requiredIds.Count;
            }
            else
            {
                duesRequirementMet = memberEntity.IsMembershipActive;
            }

            var institutionForPolicy = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            var isActive = MembershipActivityCalculator.ResolveActive(institutionForPolicy?.MemberActivePolicy, memberEntity.Status, duesRequirementMet);
            if (!isActive)
            {
                var message = institutionForPolicy?.MemberActivePolicy == MembershipActivityCalculator.ApprovedOnlyPolicy
                    ? "Your membership must be approved to submit a spotlight."
                    : "Active membership is required to submit a spotlight.";
                return ApiResponseExtensions.ToBadRequestApiResponse<SpotlightDto>(message);
            }

            var spotlight = new Spotlight
            {
                MemberId = member.Id,
                Member = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                },
                Title = request.Title,
                Story = request.Story,
                ImageUrl = request.ImageUrl,
                Status = "Pending",
                CreatedBy = member.Id,
            };

            await spotlightRepo.AddAsync(spotlight);
            return spotlight.ToDto().ToCreatedApiResponse("Spotlight submitted for review.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error submitting spotlight for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to submit spotlight");
        }
    }

    public async Task<IApiResponse<List<SpotlightDto>>> GetMySpotlightsAsync(string memberId)
    {
        try
        {
            var spotlights = await spotlightRepo.GetAllAsync(s => s.MemberId == memberId);
            var dtos = spotlights.Select(s => s.ToDto()).OrderByDescending(s => s.CreatedAt).ToList();

            // Always the same one member (the caller) — a single lookup, not a batch.
            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is not null)
            {
                foreach (var dto in dtos)
                {
                    dto.MemberName = $"{member.FirstName} {member.LastName}";
                    dto.MemberProfilePictureUrl = member.ProfilePictureUrl;
                }
            }

            return dtos.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving spotlights for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<SpotlightDto>>("Failed to retrieve spotlights");
        }
    }
}
