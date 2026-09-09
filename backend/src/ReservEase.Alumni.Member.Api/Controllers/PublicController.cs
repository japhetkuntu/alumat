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
            institution.PromptMembershipActivationAtSignup,
            institution.DisabledFeatures,
            institution.LandingPageStories,
            institution.NewsBanner,
            institution.HeroImageUrls,
            institution.HeroHeadline);

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
                .OrderBy(e => e.StartDate)
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
            var items = await spotlightRepo.GetQueryable(s => s.Status == "Approved")
                .OrderByDescending(s => s.FeaturedMonth)
                .ThenByDescending(s => s.CreatedAt)
                .Take(MaxCacheableItems)
                .ToListAsync();

            return items.Select(s => new PublicSpotlightItemResponse(
                s.Id, s.Title, s.Story, s.ImageUrl ?? s.Member?.ProfilePictureUrl,
                s.Member is { } m ? $"{m.FirstName} {m.LastName}".Trim() : "Alumni",
                s.FeaturedMonth)).ToList();
        });

        return Ok(new ApiResponse<List<PublicSpotlightItemResponse>> { Message = "Success", Code = 200, Data = all.Take(Math.Clamp(take, 1, MaxCacheableItems)).ToList() });
    }
}
