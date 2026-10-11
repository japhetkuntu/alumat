using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>
/// Real community data for the assistant. A WhatsApp webhook has no tenant, so each method first pins the tenant to the community
/// being asked about; after that the same tenant-scoped services the member portal calls do the work.
///
/// Public methods query only what an anonymous portal visitor can see (institution-wide, no community, no year-group
/// restriction, published). Member methods call the portal's own list services, which apply the community and year-group rules,
/// so nothing about visibility is re-implemented here. By-id service methods are deliberately avoided: they are weaker than the
/// list methods, and the assistant only ever links to the portal page, which enforces access itself.
/// </summary>
public class AssistantContent(
    ICurrentTenantService tenant,
    ICampaignService campaignService,
    IMemberEventService eventService,
    IMemberJobService jobService,
    IMemberNewsService newsService,
    IContributionService contributionService,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<Institution> institutionRepo) : IAssistantContent
{
    private const int Take = 5;

    private void Pin(AssistantCommunity c) => tenant.SetInstitutionId(c.InstitutionId, c.Slug);

    // ── Public ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AssistantItem>> PublicCampaignsAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        Pin(c);
        var now = DateTime.UtcNow;
        var open = await campaignRepo.GetQueryable(x => x.Status == CampaignStatus.Active && x.CommunityId == null && !x.IsMembershipCampaign && x.Deadline >= now)
            .OrderBy(x => x.Deadline).Take(20).ToListAsync(ct);
        // The public-page switch is stored inside a JSON column, so it is applied here.
        return open.Where(x => x.PublicPage is { IsPublished: true }).Take(Take)
            .Select(x => new AssistantItem(x.Title, $"Closes {x.Deadline:d MMM yyyy}", $"{c.PortalUrl}/fundraiser/{x.Id}")).ToList();
    }

    public async Task<IReadOnlyList<AssistantItem>> PublicEventsAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        Pin(c);
        var now = DateTime.UtcNow;
        var items = await eventRepo.GetQueryable(e => e.Status == "Upcoming" && e.CommunityId == null && (e.YearGroups == null || e.YearGroups.Count == 0) && e.StartDate >= now)
            .OrderBy(e => e.StartDate).Take(Take).ToListAsync(ct);
        return items.Select(e => new AssistantItem(e.Title, EventLine(e.StartDate, e.Venue), $"{c.PortalUrl}/events/{e.Id}")).ToList();
    }

    public async Task<IReadOnlyList<AssistantItem>> PublicJobsAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        Pin(c);
        var now = DateTime.UtcNow;
        var items = await jobRepo.GetQueryable(j => j.Status == "Active" && j.CommunityId == null && (j.YearGroups == null || j.YearGroups.Count == 0)
                && (j.Deadline == null || j.Deadline >= now))
            .OrderByDescending(j => j.CreatedAt).Take(Take).ToListAsync(ct);
        return items.Select(j => new AssistantItem(j.Title, JobLine(j.Company, j.Location), $"{c.PortalUrl}/jobs/{j.Id}")).ToList();
    }

    public async Task<IReadOnlyList<AssistantItem>> PublicUpdatesAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        Pin(c);
        var items = await newsRepo.GetQueryable(n => n.Status == "Published" && n.CommunityId == null && (n.YearGroups == null || n.YearGroups.Count == 0))
            .OrderByDescending(n => n.PublishedAt).Take(Take).ToListAsync(ct);
        return items.Select(n => new AssistantItem(n.Title, n.PublishedAt.HasValue ? $"{n.PublishedAt:d MMM yyyy}" : null, $"{c.PortalUrl}/news/{n.Id}")).ToList();
    }

    // ── Verified member ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<AssistantItem>> MemberCampaignsAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        if (!c.IsMember) return [];
        Pin(c);
        var page = await campaignService.GetActiveCampaignsAsync(new BaseFilter { Page = 1, PageSize = Take }, c.MemberId!);
        return (page.Data?.Results ?? [])
            .Select(x => new AssistantItem(x.Title, x.IsMembershipCampaign ? "Membership dues" : $"Closes {x.Deadline:d MMM yyyy}", $"{c.PortalUrl}/payment-campaign/{x.Id}")).ToList();
    }

    public async Task<IReadOnlyList<AssistantItem>> MemberEventsAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        if (!c.IsMember) return [];
        Pin(c);
        var page = await eventService.GetEventsAsync(new EventFilter { Page = 1, PageSize = Take, Status = "Upcoming" }, c.MemberId!);
        return (page.Data?.Results ?? []).Select(e => new AssistantItem(e.Title, EventLine(e.StartDate, e.Venue), $"{c.PortalUrl}/events/{e.Id}", e.Id)).ToList();
    }

    public async Task<IReadOnlyList<AssistantItem>> MemberJobsAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        if (!c.IsMember) return [];
        Pin(c);
        var page = await jobService.GetJobsAsync(new JobFilter { Page = 1, PageSize = Take }, c.MemberId!);
        return (page.Data?.Results ?? []).Select(j => new AssistantItem(j.Title, JobLine(j.Company, j.Location), $"{c.PortalUrl}/jobs/{j.Id}")).ToList();
    }

    public async Task<IReadOnlyList<AssistantItem>> MemberUpdatesAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        if (!c.IsMember) return [];
        Pin(c);
        var page = await newsService.GetPostsAsync(new NewsFilter { Page = 1, PageSize = Take }, c.MemberId!);
        return (page.Data?.Results ?? []).Select(n => new AssistantItem(n.Title, null, $"{c.PortalUrl}/news/{n.Id}")).ToList();
    }

    public async Task<AssistantDues?> MemberDuesAsync(AssistantCommunity c, CancellationToken ct = default)
    {
        if (!c.IsMember) return null;
        Pin(c);
        var status = await contributionService.GetMembershipStatusAsync(new AuthData { Id = c.MemberId!, Role = "Member" });
        if (status.Data is null) return null;

        var unpaid = status.Data.ArrearsCount + (status.Data.IsCurrentYearPaid ? 0 : 1);
        return new AssistantDues(status.Data.IsCurrentYearPaid, status.Data.HasArrears, unpaid, $"{c.PortalUrl}/contributions");
    }

    public async Task<bool> IsAvailableAsync(AssistantCommunity c, string feature, CancellationToken ct = default)
    {
        var institution = await institutionRepo.GetOneAsync(i => i.Id == c.InstitutionId, ignoreQueryFilters: true);
        return institution is not null && !institution.DisabledFeatures.Contains(feature);
    }

    private static string EventLine(DateTime start, string venue)
        => string.IsNullOrWhiteSpace(venue) ? start.ToString("ddd d MMM, h:mm tt", CultureInfo.InvariantCulture) : $"{start.ToString("ddd d MMM, h:mm tt", CultureInfo.InvariantCulture)} · {venue}";

    private static string JobLine(string company, string location)
        => string.IsNullOrWhiteSpace(location) ? company : $"{company} · {location}";
}
