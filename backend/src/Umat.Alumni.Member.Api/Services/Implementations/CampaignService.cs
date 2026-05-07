using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Extensions;
using Umat.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class CampaignService(
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    ILogger<CampaignService> logger) : ICampaignService
{
    public async Task<IApiResponse<PgPagedResult<CampaignDto>>> GetActiveCampaignsAsync(BaseFilter filter, string memberId)
    {
        try
        {
            logger.LogInformation("GetActiveCampaigns request — filter: {Filter} for member {MemberId}", filter.Serialize(), memberId);

            var member = await memberRepo.GetByIdAsync(memberId);
            var memberYear = member?.GraduationYear;

            var result = await campaignRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                c => c.Status == CampaignStatus.Active
                      && (c.YearGroups == null || c.YearGroups.Count == 0 || (memberYear.HasValue && c.YearGroups.Contains(memberYear.Value)))
                      && (!c.IsMembershipCampaign || !c.MembershipYear.HasValue || !memberYear.HasValue || c.MembershipYear.Value >= memberYear.Value)
            );

            var dtos = result.Results.Select(c => c.ToDto()).ToList();

            // Populate TotalEligibleMembers for membership campaigns (only members whose graduation year <= campaign's membership year)
            foreach (var dto in dtos.Where(d => d.IsMembershipCampaign && d.MembershipYear.HasValue))
            {
                var year = dto.MembershipYear!.Value;
                dto.TotalEligibleMembers = await memberRepo.CountAsync(m => m.Status == "Active" && m.GraduationYear <= year);
            }

            var dtoResult = new PgPagedResult<CampaignDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = dtos,
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving active campaigns");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<CampaignDto>>("Failed to retrieve campaigns");
        }
    }

    public async Task<IApiResponse<CampaignDto>> GetCampaignByIdAsync(string campaignId)
    {
        try
        {
            logger.LogInformation("GetCampaignById for campaignId: {CampaignId}", campaignId);

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            var dto = campaign.ToDto();

            if (dto.IsMembershipCampaign && dto.MembershipYear.HasValue)
            {
                var year = dto.MembershipYear.Value;
                dto.TotalEligibleMembers = await memberRepo.CountAsync(m => m.Status == "Active" && m.GraduationYear <= year);
            }

            return dto.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto>("Failed to retrieve campaign");
        }
    }

    public async Task<IApiResponse<CampaignDto?>> GetCurrentMembershipCampaignAsync()
    {
        try
        {
            var currentYear = DateTime.UtcNow.Year;
            logger.LogInformation("GetCurrentMembershipCampaign for year: {Year}", currentYear);

            var campaign = await campaignRepo.GetOneAsync(
                c => c.IsMembershipCampaign
                     && c.Status == CampaignStatus.Active
                     && c.MembershipYear == currentYear);

            if (campaign is null)
                return ((CampaignDto?)null).ToOkApiResponse();

            return ((CampaignDto?)campaign.ToDto()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving current membership campaign");
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto?>("Failed to retrieve current membership campaign");
        }
    }
}
