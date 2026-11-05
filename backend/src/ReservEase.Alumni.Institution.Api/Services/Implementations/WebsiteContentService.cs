using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Text;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// What the website's live sections would show a visitor right now, so the editor can show the same instead of made-up examples.
/// The rules mirror the public site (Member.Api PublicController.GetPublicSiteData): only items the institution made public, only
/// features that are switched on, institution-wide items only. Not cached: an admin editing the page wants to see a change at once.
/// </summary>
public class WebsiteContentService(
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<BusinessListing> businessRepo,
    IAlumniPgRepository<ServiceType> serviceRepo,
    IAlumniPgRepository<StoreProduct> productRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    ICurrentTenantService currentTenant,
    ILogger<WebsiteContentService> logger) : IWebsiteContentService
{
    private const int Take = 6;

    public async Task<IApiResponse<WebsiteContentDto>> GetAsync()
    {
        try
        {
            var institution = currentTenant.InstitutionId is { } id ? await institutionRepo.GetByIdAsync(id, ignoreQueryFilters: true) : null;
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<WebsiteContentDto>("Institution not found");
            var off = institution.DisabledFeatures ?? [];
            bool On(string f) => !off.Contains(f);
            var now = DateTime.UtcNow;

            var news = !On(InstitutionFeatures.News) ? [] : (await newsRepo.GetQueryable(n => n.Status == "Published" && n.CommunityId == null && (n.YearGroups == null || n.YearGroups.Count == 0))
                    .OrderByDescending(n => n.PublishedAt).Take(Take).ToListAsync())
                .Select(n => new WebsiteNewsItem(n.Id, n.Title, RichText.Excerpt(n.Content, 180), n.ImageUrls?.FirstOrDefault(), n.PublishedAt, n.Category)).ToList();

            var upcoming = await eventRepo.GetQueryable(e => e.Status == "Upcoming" && e.CommunityId == null && (e.YearGroups == null || e.YearGroups.Count == 0) && e.StartDate >= now)
                .OrderBy(e => e.StartDate).Take(50).ToListAsync();
            var events = !On(InstitutionFeatures.Events) ? [] : upcoming.Take(Take).Select(e => new WebsiteEventItem(e.Id, e.Title, e.StartDate, e.Venue, e.BannerImageUrl)).ToList();

            List<WebsiteCommunityItem> communities = [];
            if (On(InstitutionFeatures.Communities))
            {
                var groups = await communityRepo.GetQueryable(c => c.IsActive && c.IsPublic).OrderBy(c => c.Name).Take(Take).ToListAsync();
                var ids = groups.Select(g => g.Id).ToList();
                var counts = await membershipRepo.GetQueryable(m => ids.Contains(m.CommunityId) && m.Status == "Approved")
                    .GroupBy(m => m.CommunityId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
                communities = groups.Select(g => new WebsiteCommunityItem(g.Id, g.Name, counts.GetValueOrDefault(g.Id), RichText.Excerpt(g.Description, 160), g.CoverImageUrl)).ToList();
            }

            var businesses = !On(InstitutionFeatures.BusinessDirectory) ? [] : (await businessRepo.GetQueryable(b => b.Status == "Approved" && !b.IsHiddenByMember)
                    .OrderByDescending(b => b.CreatedAt).Take(Take).ToListAsync())
                .Select(b => new WebsiteBusinessItem(b.Id, b.BusinessName, b.Location, b.LogoUrl, b.BannerUrl)).ToList();

            var services = !On(InstitutionFeatures.Services) ? [] : (await serviceRepo.GetQueryable(s => s.Status == "Active" && s.ShowOnWebsite && s.CommunityId == null)
                    .OrderBy(s => s.Name).Take(Take).ToListAsync())
                .Select(s => new WebsiteServiceItem(s.Id, s.Name, RichText.Excerpt(s.Description, 140), s.Price, s.ImageUrl)).ToList();

            var store = !On(InstitutionFeatures.Store) ? [] : (await productRepo.GetQueryable(p => p.Status == "Active" && p.ShowOnWebsite)
                    .OrderByDescending(p => p.CreatedAt).Take(Take).ToListAsync())
                .Select(p => new WebsiteProductItem(p.Id, p.Name, p.Price, p.ImageUrls?.FirstOrDefault())).ToList();

            var fundraisers = !On(InstitutionFeatures.Contributions) ? [] : (await campaignRepo.GetQueryable(c => c.Status == CampaignStatus.Active && !c.IsMembershipCampaign && c.CommunityId == null
                        && c.PublicPage != null && c.PublicPage.IsPublished && (c.YearGroups == null || c.YearGroups.Count == 0) && c.Deadline >= now)
                    .OrderBy(c => c.Deadline).Take(Take).ToListAsync())
                .Select(c => new WebsiteFundraiserItem(c.Id, c.Title, RichText.Excerpt(c.Description, 140), c.PublicPage!.ShowTotalRaised ? c.CollectedAmount : 0, c.PublicPage.ShowTarget ? c.TargetAmount : 0, c.BannerImageUrl)).ToList();

            var jobs = !On(InstitutionFeatures.Jobs) ? [] : (await jobRepo.GetQueryable(j => j.Status == "Active" && j.IsPublic && j.CommunityId == null && (j.YearGroups == null || j.YearGroups.Count == 0)
                        && (j.Deadline == null || j.Deadline >= now))
                    .OrderByDescending(j => j.CreatedAt).Take(Take).ToListAsync())
                .Select(j => new WebsiteJobItem(j.Id, j.Title, j.Company, j.Location)).ToList();

            var spotlights = !On(InstitutionFeatures.Spotlights) ? [] : (await spotlightRepo.GetQueryable(s => s.Status == "Approved" && (s.Type != "Birthday" || s.CreatedAt >= DateTime.UtcNow.AddHours(-72))) // birthdays leave after 72 hours, as on the live site
                    .OrderByDescending(s => s.IsFeatured).ThenByDescending(s => s.FeaturedMonth).ThenByDescending(s => s.CreatedAt).Take(Take).ToListAsync())
                .Select(s => new WebsiteSpotlightItem(s.Id, s.Title, s.Story, s.ImageUrl ?? s.Member?.ProfilePictureUrl, s.Member is { } m ? $"{m.FirstName} {m.LastName}".Trim() : "Member")).ToList();

            var members = await memberRepo.CountAsync(m => m.Status == "Active");
            var joined = await memberRepo.CountAsync(m => m.Status == "Active" && m.CreatedAt >= now.AddDays(-30));
            int? openJobs = On(InstitutionFeatures.Jobs) ? await jobRepo.CountAsync(j => j.Status == "Active" && j.CommunityId == null) : null;
            int? bizCount = On(InstitutionFeatures.BusinessDirectory) ? await businessRepo.CountAsync(b => b.Status == "Approved" && !b.IsHiddenByMember) : null;
            var stats = new WebsiteStats(members, joined, On(InstitutionFeatures.Events) ? upcoming.Count(e => e.StartDate <= now.AddDays(30)) : null, openJobs, bizCount, communities.Count);

            return new WebsiteContentDto(news, events, communities, businesses, services, store, fundraisers, jobs, spotlights, stats).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading the website's content");
            return ApiResponseExtensions.ToServerErrorApiResponse<WebsiteContentDto>("Failed to load your items");
        }
    }
}
