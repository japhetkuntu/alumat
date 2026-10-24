using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
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
    ILogger<CampaignService> logger,
    Microsoft.Extensions.Caching.Memory.IMemoryCache? cache = null) : ICampaignService
{
    /// <summary>How long the numbers and names on a public fundraiser page are reused. Short enough that a gift shows up almost at once, long enough that a shared link going viral costs one set of queries every few seconds instead of one per visitor.</summary>
    private static readonly TimeSpan PublicPageCacheFor = TimeSpan.FromSeconds(10);

    private sealed record FundraiserTotals(decimal Raised, int ContributorCount, List<string> Names);

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
            // Membership dues are owed from the graduation year, or from the year of joining where there is none.
            int? memberFromYear = member is null ? null : MembershipPeriods.RequiredFromYear(member.GraduationYear, member.CreatedAt);

            var approvedCommunityIds = string.IsNullOrEmpty(communityId) ? await GetApprovedCommunityIdsAsync(memberId) : [];

            var result = await campaignRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                c => c.Status == CampaignStatus.Active
                      && (string.IsNullOrEmpty(communityId)
                          ? (c.CommunityId == null || approvedCommunityIds.Contains(c.CommunityId))
                          : c.CommunityId == communityId)
                      && (c.YearGroups == null || c.YearGroups.Count == 0 || (memberYear.HasValue && c.YearGroups.Contains(memberYear.Value)))
                      && (!c.IsMembershipCampaign || !c.MembershipYear.HasValue || !memberFromYear.HasValue || c.MembershipYear.Value >= memberFromYear.Value)
            );

            var dtos = result.Results.Select(c => c.ToDto()).ToList();

            // Populate TotalEligibleMembers for membership campaigns (only members whose graduation year <= campaign's membership year).
            // Cached per distinct year within this call, instead of one query per campaign row.
            var eligibleCountsByYear = new Dictionary<int, int>();
            foreach (var dto in dtos.Where(d => d.IsMembershipCampaign && d.MembershipYear.HasValue))
            {
                var year = dto.MembershipYear!.Value;
                if (!eligibleCountsByYear.TryGetValue(year, out var count))
                {
                    count = await memberRepo.CountAsync(m => m.Status == "Active" && m.GraduationYear <= year);
                    eligibleCountsByYear[year] = count;
                }
                dto.TotalEligibleMembers = count;
                dto.TargetAmount = MembershipTarget.For(dto.AmountPerMember, count);
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
                dto.TargetAmount = MembershipTarget.For(dto.AmountPerMember, dto.TotalEligibleMembers.Value);
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
            // Only the few columns the wall shows are read, not whole contribution rows.
            var contributions = (await contributionRepo.GetQueryable(c => c.CampaignId == campaignId && c.Status == "Successful" && c.ShowOnWallOfSupport)
                    .Select(c => new { c.MemberId, c.Member, At = c.ConfirmedAt ?? c.CreatedAt })
                    .ToListAsync())
                .Where(c => c.Member is not null)
                .ToList();

            // The name here comes from the MemberSnapshot frozen on the contribution
            // at payment time — refresh it from the live Member records (one batched
            // lookup, not one query per row) so a later name change actually shows
            // up. Falls back to the snapshot if the member has since been deleted.
            var contributorIds = contributions.Select(c => c.MemberId).Distinct().ToList();
            var contributors = await LiveNamesAsync(contributorIds);

            var entries = contributions
                .OrderByDescending(c => c.At)
                .Select(c => new WallOfSupportEntryDto
                {
                    Name = (contributors.TryGetValue(c.MemberId, out var m)
                        ? $"{m.FirstName} {m.LastName}"
                        : $"{c.Member!.FirstName} {c.Member.LastName}").Trim(),
                    ContributedAt = c.At,
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

    /// <summary>
    /// The anonymous, shareable page for a fundraiser an admin has published. 404 whenever it isn't published
    /// (or isn't an institution-wide fundraiser) so the existence of unpublished fundraisers never leaks.
    /// Only names and the aggregates the admin chose to show leave this method — never any individual amount.
    /// </summary>
    public async Task<IApiResponse<PublicFundraiserDto>> GetPublicFundraiserAsync(string campaignId)
    {
        try
        {
            var campaign = await campaignRepo.GetOneAsync(c => c.Id == campaignId);
            var page = campaign?.PublicPage;
            if (campaign is null || page is not { IsPublished: true }
                || campaign.IsMembershipCampaign || campaign.CommunityId is not null
                || campaign.Status == CampaignStatus.Archived)
                return ApiResponseExtensions.ToNotFoundApiResponse<PublicFundraiserDto>("This fundraiser page isn't available.");

            var totals = await GetFundraiserTotalsAsync(campaign.Id, page.NamePolicy);
            var raised = totals.Raised;
            var dto = new PublicFundraiserDto
            {
                Id = campaign.Id,
                Title = campaign.Title,
                Description = campaign.Description,
                BannerImageUrl = campaign.BannerImageUrl,
                YoutubeVideoUrl = campaign.YoutubeVideoUrl,
                Message = page.Message,
                IsOpenForGiving = campaign.Status == CampaignStatus.Active && campaign.Deadline >= DateTime.UtcNow,
                TotalRaised = page.ShowTotalRaised ? raised : null,
                TargetAmount = page.ShowTarget ? campaign.TargetAmount : null,
                ProgressPercent = page.ShowProgress && campaign.TargetAmount > 0
                    ? (int)Math.Min(100, Math.Floor(raised / campaign.TargetAmount * 100)) : null,
                ContributorCount = page.ShowContributorCount ? totals.ContributorCount : null,
                Deadline = page.ShowDeadline ? campaign.Deadline : null,
                Contributors = totals.Names,
            };
            return dto.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving public fundraiser {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PublicFundraiserDto>("Failed to load this fundraiser page");
        }
    }

    /// <summary>Live first and last names for a set of members: only those two columns, not whole member rows.</summary>
    private async Task<Dictionary<string, (string FirstName, string LastName)>> LiveNamesAsync(List<string> ids)
    {
        if (ids.Count == 0) return [];
        var rows = await memberRepo.GetQueryable(m => ids.Contains(m.Id)).Select(m => new { m.Id, m.FirstName, m.LastName }).ToListAsync();
        return rows.ToDictionary(m => m.Id, m => (m.FirstName, m.LastName));
    }

    /// <summary>
    /// What a public fundraiser page shows that comes from the gifts: the total, how many people gave, and the names. The total and the
    /// count are worked out in the database, so a campaign with thousands of gifts does not pull thousands of rows to add them up, and
    /// the result is reused for a few seconds so many visitors at once share one set of queries.
    /// </summary>
    private async Task<FundraiserTotals> GetFundraiserTotalsAsync(string campaignId, string policy)
    {
        var key = $"public-fundraiser:{campaignId}:{policy}";
        if (cache is not null && cache.TryGetValue(key, out FundraiserTotals? hit) && hit is not null) return hit;

        var successful = contributionRepo.GetQueryable(c => c.CampaignId == campaignId && c.Status == "Successful");
        var raised = await successful.SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var contributorCount = await successful.Select(c => string.IsNullOrEmpty(c.MemberId) ? c.Id : c.MemberId).Distinct().CountAsync();

        var named = (await (policy == PublicNamePolicy.Everyone ? successful : successful.Where(c => c.ShowOnWallOfSupport))
                .OrderBy(c => c.ConfirmedAt ?? c.CreatedAt).ThenBy(c => c.Id)
                .Select(c => new { c.MemberId, c.Member })
                .ToListAsync())
            .Where(c => c.Member is not null)
            .ToList();

        var ids = named.Select(c => c.MemberId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var live = await LiveNamesAsync(ids);

        // One entry per person, oldest giver first so the page reads as the order people showed up.
        var names = named
            .Select(c => (Key: string.IsNullOrEmpty(c.MemberId) ? $"{c.Member!.FirstName} {c.Member.LastName}" : c.MemberId,
                          Name: (live.TryGetValue(c.MemberId, out var m) ? $"{m.FirstName} {m.LastName}" : $"{c.Member!.FirstName} {c.Member.LastName}").Trim()))
            .Where(x => x.Name.Length > 0)
            .DistinctBy(x => x.Key)
            .Select(x => x.Name)
            .ToList();

        var totals = new FundraiserTotals(raised, contributorCount, names);
        cache?.Set(key, totals, PublicPageCacheFor);
        return totals;
    }
}
