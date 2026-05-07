using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class SpotlightService(
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
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

            // Populate graduation year from member records
            var memberIds = dtos.Select(d => d.MemberId).Distinct().ToList();
            var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtos)
            {
                if (members.TryGetValue(dto.MemberId, out var m))
                    dto.MemberGraduationYear = m.GraduationYear;
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
                dto.MemberGraduationYear = member.GraduationYear;

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

            // Compute active membership dynamically: paid all campaigns from grad year through current year
            var currentYear = DateTime.UtcNow.Year;
            var requiredCampaigns = await campaignRepo.GetAllAsync(c =>
                c.IsMembershipCampaign && c.MembershipYear.HasValue
                && c.MembershipYear.Value >= memberEntity.GraduationYear
                && c.MembershipYear.Value <= currentYear);
            if (requiredCampaigns.Any())
            {
                var requiredIds = requiredCampaigns.Select(c => c.Id).ToHashSet();
                var confirmed = await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && c.Status == "Confirmed");
                var paidCount = confirmed.Count(c => requiredIds.Contains(c.CampaignId));
                if (paidCount < requiredIds.Count)
                    return ApiResponseExtensions.ToBadRequestApiResponse<SpotlightDto>("Active membership is required to submit a spotlight.");
            }
            else if (!memberEntity.IsMembershipActive)
                return ApiResponseExtensions.ToBadRequestApiResponse<SpotlightDto>("Active membership is required to submit a spotlight.");

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
            return spotlights.Select(s => s.ToDto()).OrderByDescending(s => s.CreatedAt).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving spotlights for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<SpotlightDto>>("Failed to retrieve spotlights");
        }
    }
}
