using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class CampaignService(
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<CampaignUpdate> updateRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    ILogger<CampaignService> logger) : ICampaignService
{
    private async Task<bool> IsApprovedCommunityMemberAsync(string communityId, string memberId)
    {
        var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
        return membership is not null && membership.Status == "Approved";
    }

    private async Task<List<string>> GetApprovedCommunityIdsAsync(string memberId)
    {
        var memberships = await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved");
        return memberships.Select(m => m.CommunityId).ToList();
    }

    private async Task EnrichCommunityNamesAsync(List<CampaignDto> results)
    {
        var communityIds = results.Where(d => d.CommunityId != null).Select(d => d.CommunityId!).Distinct().ToList();
        if (communityIds.Count == 0) return;
        var communities = await communityRepo.GetAllAsync(c => communityIds.Contains(c.Id));
        var nameById = communities.ToDictionary(c => c.Id, c => c.Name);
        foreach (var dto in results)
            if (dto.CommunityId != null && nameById.TryGetValue(dto.CommunityId, out var name))
                dto.CommunityName = name;
    }

    public async Task<IApiResponse<PgPagedResult<CampaignDto>>> GetActiveCampaignsAsync(BaseFilter filter, string memberId, string? communityId = null)
    {
        try
        {
            logger.LogInformation("GetActiveCampaigns request — filter: {Filter} for member {MemberId}", filter.Serialize(), memberId);

            if (!string.IsNullOrEmpty(communityId) && !await IsApprovedCommunityMemberAsync(communityId, memberId))
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<CampaignDto>>("You must be an approved member of this community to view its campaigns");

            var member = await memberRepo.GetByIdAsync(memberId);
            var memberYear = member?.GraduationYear;

            var approvedCommunityIds = string.IsNullOrEmpty(communityId) ? await GetApprovedCommunityIdsAsync(memberId) : [];

            var result = await campaignRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                c => c.Status == CampaignStatus.Active
                      && (string.IsNullOrEmpty(communityId)
                          ? (c.CommunityId == null || approvedCommunityIds.Contains(c.CommunityId))
                          : c.CommunityId == communityId)
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

            await EnrichCommunityNamesAsync(dtos);

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

    public async Task<IApiResponse<CampaignDto>> GetCampaignByIdAsync(string campaignId, string? memberId = null)
    {
        try
        {
            logger.LogInformation("GetCampaignById for campaignId: {CampaignId}", campaignId);

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            // Community campaigns are private — anonymous callers and non-members
            // get the same "not found" as a genuinely missing campaign, rather
            // than a 403 that would confirm the campaign exists.
            if (!string.IsNullOrEmpty(campaign.CommunityId)
                && (string.IsNullOrEmpty(memberId) || !await IsApprovedCommunityMemberAsync(campaign.CommunityId, memberId)))
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

    public async Task<IApiResponse<List<CampaignUpdateDto>>> GetUpdatesAsync(string campaignId)
    {
        try
        {
            var updates = await updateRepo.GetAllAsync(u => u.CampaignId == campaignId);
            var dtos = updates.OrderByDescending(u => u.CreatedAt).Select(u => u.ToDto()).ToList();
            return dtos.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving updates for campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CampaignUpdateDto>>("Failed to retrieve updates");
        }
    }

    public async Task<IApiResponse<List<WallOfSupportEntryDto>>> GetWallOfSupportAsync(string campaignId)
    {
        try
        {
            var contributions = await contributionRepo.GetAllAsync(
                c => c.CampaignId == campaignId && c.Status == "Confirmed" && c.ShowOnWallOfSupport);
            var entries = contributions
                .Where(c => c.Member is not null)
                .OrderByDescending(c => c.ConfirmedAt ?? c.CreatedAt)
                .Select(c => new WallOfSupportEntryDto
                {
                    Name = $"{c.Member!.FirstName} {c.Member.LastName}".Trim(),
                    ContributedAt = c.ConfirmedAt ?? c.CreatedAt,
                })
                .ToList();
            return entries.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving wall of support for campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<WallOfSupportEntryDto>>("Failed to retrieve wall of support");
        }
    }
}
