using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// Unauthenticated endpoints safe to call before login — used by the
/// Member Portal frontend to theme itself for the resolved tenant, and by
/// the public marketing site (also served from this app — see
/// platform-marketing-page.tsx) for the institution-onboarding form.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
public class PublicController(
    IAlumniPgRepository<OnboardingLead> onboardingLeadRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<BusinessListing> businessListingRepo,
    IAlumniPgRepository<PlatformStaff> platformStaffRepo,
    IAlumniPgRepository<PlatformNotification> platformNotificationRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<Resource> resourceRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<PhotoAlbum> albumRepo,
    IAlumniPgRepository<ServiceType> serviceTypeRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IRedisService<PublicContentCacheConfig> publicCache) : DefaultController
{
    /// <summary>Cache the widest reasonable slice once per institution (rather than one cache entry per `take` value) so every caller's request, whatever `take` it asks for, hits the same cached list — Institution.Api invalidates exactly one key per resource on any admin edit, see PublicContentCacheKeys.</summary>
    private const int MaxCacheableItems = 12;

    private async Task<List<T>> GetOrCacheAsync<T>(string cacheKey, Func<Task<List<T>>> loadFromDb)
    {
        var cached = await publicCache.GetAsync<List<T>>(cacheKey);
        if (cached is not null) return cached;

        var fresh = await loadFromDb();
        await publicCache.SetAsync(cacheKey, fresh);
        return fresh;
    }

    /// <summary>
    /// Branding for the institution resolved from the request's Host header
    /// (see TenantResolutionMiddleware). Falls back to the configured default
    /// tenant when no real subdomain/custom-domain match is found.
    /// </summary>
    [HttpGet("institution/theme")]
    [SwaggerOperation(Summary = "Get the current tenant's branding/theme")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionThemeResponse>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public IActionResult GetInstitutionTheme()
    {
        if (HttpContext.Items["Institution"] is not Institution institution)
            return NotFound(new ApiResponse<object> { Message = "No institution resolved for this host", Code = 404 });

        var theme = new InstitutionThemeResponse(
            institution.Slug,
            institution.PortalName,
            string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName,
            institution.PrimaryColorHex,
            institution.SecondaryColorHex,
            institution.LogoUrl,
            institution.Tagline,
            institution.IconUrl,
            institution.MemberPortalTitle,
            institution.MemberAuthHeadline,
            institution.MemberAuthSubtext,
            institution.RequireStudentId,
            institution.ProgramOfStudyEnabled,
            institution.ProgramsOfStudy,
            institution.SocialLinks,
            institution.PromptMembershipActivationAtSignup,
            institution.SmsNotificationsEnabled,
            institution.DisabledFeatures,
            institution.LandingPageStories,
            institution.NewsBanner,
            institution.HeroImageUrls,
            institution.HeroHeadline,
            institution.OrganizationType,
            institution.CohortLabel,
            institution.CohortLabelPlural);

        return Ok(new ApiResponse<InstitutionThemeResponse> { Message = "Success", Code = 200, Data = theme });
    }

    /// <summary>
    /// A prospective institution's request to be onboarded, from the public
    /// marketing site. Spam-worthy like login, so this overrides the class's
    /// read-only rate limit with the stricter auth policy.
    /// </summary>
    [HttpPost("onboarding-leads")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Submit a request to onboard a new institution")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    public async Task<IActionResult> CreateOnboardingLead([FromBody] CreateOnboardingLeadRequest request)
    {
        var lead = new OnboardingLead
        {
            InstitutionName = request.InstitutionName,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Country = request.Country,
            EstimatedMemberCount = request.EstimatedMemberCount,
            Message = request.Message,
        };
        await onboardingLeadRepo.AddAsync(lead);

        // Let the platform team know a lead came in — this is the top of the
        // "we'll build it for you" acquisition funnel, so a lead sitting
        // unseen until someone happens to check the dashboard is a real cost.
        var staff = (await platformStaffRepo.GetAllAsync(s => !s.IsDisabled && (s.Role == "SuperAdmin" || s.Role == "Support"))).ToList();
        if (staff.Count > 0)
        {
            var notifications = staff.Select(s => new PlatformNotification
            {
                RecipientStaffId = s.Id,
                Title = "New onboarding lead",
                Body = $"{lead.InstitutionName} — {lead.ContactName} ({lead.ContactEmail}) wants to get onboarded.",
                Type = "OnboardingLeadSubmitted",
                RelatedEntityId = lead.Id,
                RelatedEntityType = "OnboardingLead",
                ActionUrl = "/onboarding-leads",
                CreatedBy = "system",
            }).ToList();
            await platformNotificationRepo.AddRangeAsync(notifications);
        }

        var response = new OnboardingLeadResponse(lead.Id, lead.InstitutionName, lead.ContactEmail, lead.Status);
        return Created(string.Empty, new ApiResponse<OnboardingLeadResponse> { Message = "Created", Code = 201, Data = response });
    }

    /// <summary>
    /// Latest institution-wide published news, for the public landing page.
    /// Community/year-restricted posts are excluded — an anonymous visitor
    /// has no membership context to check them against.
    /// </summary>
    [HttpGet("news")]
    [SwaggerOperation(Summary = "Get the latest institution-wide news for the public landing page")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PublicNewsItemResponse>>))]
    public async Task<IActionResult> GetPublicNews([FromQuery] int take = 3)
    {
        if (HttpContext.Items["Institution"] is not Institution institution)
            return Ok(new ApiResponse<List<PublicNewsItemResponse>> { Message = "Success", Code = 200, Data = [] });

        var all = await GetOrCacheAsync(PublicContentCacheKeys.News(institution.Id), async () =>
        {
            var items = await newsRepo.GetQueryable(n =>
                    n.Status == "Published" && n.CommunityId == null && (n.YearGroups == null || n.YearGroups.Count == 0))
                .OrderByDescending(n => n.PublishedAt)
                .Take(MaxCacheableItems)
                .ToListAsync();

            return items.Select(n => new PublicNewsItemResponse(
                n.Id, n.Title, ExcerptFromHtml(n.Content, 180),
                n.ImageUrls?.FirstOrDefault(), n.PublishedAt, n.Category)).ToList();
        });

        return Ok(new ApiResponse<List<PublicNewsItemResponse>> { Message = "Success", Code = 200, Data = all.Take(Math.Clamp(take, 1, MaxCacheableItems)).ToList() });
    }

    /// <summary>Strips markup and collapses whitespace on rich-text content (news posts are authored via a WYSIWYG editor) so a plain-text excerpt can be shown outside the portal, then truncates on a word boundary.</summary>
    private static string ExcerptFromHtml(string html, int maxLength)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", " "));
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= maxLength) return text;
        var cut = text[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0) cut = cut[..lastSpace];
        return cut.TrimEnd() + "…";
    }

    /// <summary>Next institution-wide upcoming events, for the public landing page.</summary>
    [HttpGet("events")]
    [SwaggerOperation(Summary = "Get the next institution-wide upcoming events for the public landing page")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PublicEventItemResponse>>))]
    public async Task<IActionResult> GetPublicEvents([FromQuery] int take = 3)
    {
        if (HttpContext.Items["Institution"] is not Institution institution)
            return Ok(new ApiResponse<List<PublicEventItemResponse>> { Message = "Success", Code = 200, Data = [] });

        var all = await GetOrCacheAsync(PublicContentCacheKeys.Events(institution.Id), async () =>
        {
            var items = await eventRepo.GetQueryable(e =>
                    e.Status == "Upcoming" && e.CommunityId == null && (e.YearGroups == null || e.YearGroups.Count == 0)
                    && e.StartDate >= DateTime.UtcNow)
                .OrderByDescending(e => e.StartDate)
                .Take(MaxCacheableItems)
                .ToListAsync();

            return items.Select(e => new PublicEventItemResponse(e.Id, e.Title, e.StartDate, e.Venue, e.BannerImageUrl)).ToList();
        });

        // A cached list can go slightly stale on time alone (an event tips into the past) between
        // writes — the short cache TTL bounds that, but filter here too so a visitor never sees one.
        var upcoming = all.Where(e => e.StartDate >= DateTime.UtcNow).Take(Math.Clamp(take, 1, MaxCacheableItems)).ToList();

        return Ok(new ApiResponse<List<PublicEventItemResponse>> { Message = "Success", Code = 200, Data = upcoming });
    }

    /// <summary>Most recently featured alumni spotlight(s), for the public landing page.</summary>
    [HttpGet("spotlights")]
    [SwaggerOperation(Summary = "Get the latest approved alumni spotlight(s) for the public landing page")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PublicSpotlightItemResponse>>))]
    public async Task<IActionResult> GetPublicSpotlights([FromQuery] int take = 1)
    {
        if (HttpContext.Items["Institution"] is not Institution institution)
            return Ok(new ApiResponse<List<PublicSpotlightItemResponse>> { Message = "Success", Code = 200, Data = [] });

        var all = await GetOrCacheAsync(PublicContentCacheKeys.Spotlights(institution.Id), async () =>
        {
            // An admin-picked IsFeatured spotlight always leads (see
            // InstitutionSpotlightService.SetFeaturedAsync) — falls back to
            // the previous implicit "most recently featured" ordering when
            // nothing has been explicitly picked.
            var items = await spotlightRepo.GetQueryable(s => s.Status == "Approved")
                .OrderByDescending(s => s.IsFeatured)
                .ThenByDescending(s => s.FeaturedMonth)
                .ThenByDescending(s => s.CreatedAt)
                .Take(MaxCacheableItems)
                .ToListAsync();

            return items.Select(s => new PublicSpotlightItemResponse(
                s.Id, s.Title, s.Story, s.ImageUrl ?? s.Member?.ProfilePictureUrl,
                s.Member is { } m ? $"{m.FirstName} {m.LastName}".Trim() : "Member",
                s.FeaturedMonth)).ToList();
        });

        return Ok(new ApiResponse<List<PublicSpotlightItemResponse>> { Message = "Success", Code = 200, Data = all.Take(Math.Clamp(take, 1, MaxCacheableItems)).ToList() });
    }

    /// <summary>Approved, non-hidden alumni business listings, for the public landing page's business directory.</summary>
    [HttpGet("businesses")]
    [SwaggerOperation(Summary = "Get approved alumni business listings for the public landing page")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PublicBusinessListingItemResponse>>))]
    public async Task<IActionResult> GetPublicBusinesses([FromQuery] int take = 6)
    {
        if (HttpContext.Items["Institution"] is not Institution institution)
            return Ok(new ApiResponse<List<PublicBusinessListingItemResponse>> { Message = "Success", Code = 200, Data = [] });

        var all = await GetOrCacheAsync(PublicContentCacheKeys.Businesses(institution.Id), async () =>
        {
            var items = await businessListingRepo.GetQueryable(b => b.Status == "Approved" && !b.IsHiddenByMember)
                .OrderByDescending(b => b.CreatedAt)
                .Take(MaxCacheableItems)
                .ToListAsync();

            return items.Select(b => new PublicBusinessListingItemResponse(
                b.Id, b.BusinessName, ExcerptFromHtml(b.Description, 160), b.LogoUrl, b.BannerUrl,
                b.Location, b.WebsiteUrl, b.ExternalLinkUrl)).ToList();
        });

        return Ok(new ApiResponse<List<PublicBusinessListingItemResponse>> { Message = "Success", Code = 200, Data = all.Take(Math.Clamp(take, 1, MaxCacheableItems)).ToList() });
    }

    /// <summary>
    /// Title/description/image for a single entity, used to render real Open Graph tags
    /// on a shared detail-page link (e.g. the WhatsApp/link-unfurl preview when a member
    /// shares an event) instead of falling back to the institution's generic branding.
    /// Deliberately not year/community-restricted like the landing-page lists above: a
    /// caller here already has a specific id from a link someone shared with them — this
    /// unfurls that one link, the same way a crawler unfurls any gated document link
    /// without a session, rather than acting as a discovery/browse surface.
    /// </summary>
    [HttpGet("preview/{type}/{id}")]
    [SwaggerOperation(Summary = "Get title/description/image for a shared link's Open Graph preview")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PublicPreviewResponse>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetPublicPreview(string type, string id)
    {
        var preview = type.ToLowerInvariant() switch
        {
            "event" => await BuildEventPreviewAsync(id),
            "job" => await BuildJobPreviewAsync(id),
            "news" => await BuildNewsPreviewAsync(id),
            "resource" => await BuildResourcePreviewAsync(id),
            "business" => await BuildBusinessPreviewAsync(id),
            "community" => await BuildCommunityPreviewAsync(id),
            "album" => await BuildAlbumPreviewAsync(id),
            "service" => await BuildServicePreviewAsync(id),
            "campaign" => await BuildCampaignPreviewAsync(id),
            _ => null,
        };

        if (preview is null)
            return NotFound(new ApiResponse<object> { Message = "Not found", Code = 404 });

        return Ok(new ApiResponse<PublicPreviewResponse> { Message = "Success", Code = 200, Data = preview });
    }

    private async Task<PublicPreviewResponse?> BuildEventPreviewAsync(string id)
    {
        var e = await eventRepo.GetOneAsync(x => x.Id == id);
        return e is null ? null : new PublicPreviewResponse(e.Title, e.Description, e.BannerImageUrl);
    }

    private async Task<PublicPreviewResponse?> BuildJobPreviewAsync(string id)
    {
        var j = await jobRepo.GetOneAsync(x => x.Id == id);
        return j is null ? null : new PublicPreviewResponse($"{j.Title} at {j.Company}", j.Description, j.BannerImageUrl);
    }

    private async Task<PublicPreviewResponse?> BuildNewsPreviewAsync(string id)
    {
        var n = await newsRepo.GetOneAsync(x => x.Id == id);
        return n is null ? null : new PublicPreviewResponse(n.Title, ExcerptFromHtml(n.Content, 180), n.ImageUrls?.FirstOrDefault());
    }

    private async Task<PublicPreviewResponse?> BuildResourcePreviewAsync(string id)
    {
        var r = await resourceRepo.GetOneAsync(x => x.Id == id);
        return r is null ? null : new PublicPreviewResponse(r.Title, r.Description, r.BannerImageUrl);
    }

    private async Task<PublicPreviewResponse?> BuildBusinessPreviewAsync(string id)
    {
        var b = await businessListingRepo.GetOneAsync(x => x.Id == id);
        return b is null ? null : new PublicPreviewResponse(b.BusinessName, ExcerptFromHtml(b.Description, 160), b.BannerUrl ?? b.LogoUrl);
    }

    private async Task<PublicPreviewResponse?> BuildCommunityPreviewAsync(string id)
    {
        var c = await communityRepo.GetOneAsync(x => x.Id == id);
        return c is null ? null : new PublicPreviewResponse(c.Name, c.Description, c.CoverImageUrl);
    }

    private async Task<PublicPreviewResponse?> BuildAlbumPreviewAsync(string id)
    {
        var a = await albumRepo.GetOneAsync(x => x.Id == id);
        return a is null ? null : new PublicPreviewResponse(a.Title, a.Description, a.CoverImageUrl);
    }

    private async Task<PublicPreviewResponse?> BuildServicePreviewAsync(string id)
    {
        var s = await serviceTypeRepo.GetOneAsync(x => x.Id == id);
        return s is null ? null : new PublicPreviewResponse(s.Name, s.Description, null);
    }

    private async Task<PublicPreviewResponse?> BuildCampaignPreviewAsync(string id)
    {
        var c = await campaignRepo.GetOneAsync(x => x.Id == id);
        return c is null ? null : new PublicPreviewResponse(c.Title, c.Description, c.BannerImageUrl);
    }
}

public record PublicPreviewResponse(string Title, string? Description, string? ImageUrl);
