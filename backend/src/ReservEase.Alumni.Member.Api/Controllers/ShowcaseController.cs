using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>One featured institution as the public website shows it. A number is null unless the institution agreed to show it, and zero is never shown.</summary>
public record ShowcaseInstitution(string Name, string Slug, string? LogoUrl, string? PrimaryColor, string? Blurb, string OrganizationType, string? PortalUrl, int? Members, int? Groups, int? Events, decimal? Raised);

/// <summary>Totals across exactly the institutions listed, counting only the numbers each one agreed to show.</summary>
public record ShowcaseTotals(int Members, int Groups, int Events, decimal Raised);

public record ShowcaseResponse(List<ShowcaseInstitution> Institutions, ShowcaseTotals Totals);

/// <summary>The institutions AlumUnion's own website names as real users, chosen by platform staff. No sign-in, and no tenant: it is read from the bare marketing domain.</summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
public class ShowcaseController(
    IAlumniPgRepository<Institution> institutionRepo,
    ShowcaseStatsReader stats,
    IMemoryCache cache,
    IConfiguration config) : DefaultController
{
    private const string CacheKey = "public-showcase";
    /// <summary>Long enough that a busy website does not count every institution on every visit, short enough that a change made in the platform portal shows up soon.</summary>
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(10);

    [HttpGet]
    [SwaggerOperation(Summary = "The institutions featured on the AlumUnion website, with the numbers they agreed to show")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ShowcaseResponse>))]
    public async Task<IActionResult> Get()
    {
        if (!cache.TryGetValue(CacheKey, out ShowcaseResponse? response) || response is null)
        {
            response = await BuildAsync();
            cache.Set(CacheKey, response, Fresh);
        }
        return response.ToOkApiResponse().ToActionResult();
    }

    private async Task<ShowcaseResponse> BuildAsync()
    {
        var featured = await institutionRepo.GetQueryable(i => i.FeaturedOnWebsite && i.Status == "Active", ignoreQueryFilters: true)
            .OrderBy(i => i.FeaturedOrder).ThenBy(i => i.Name).ToListAsync();
        var numbers = await stats.ReadAsync(featured.Select(i => i.Id).ToList());

        static int? Shown(Institution i, string key, int value) => i.FeaturedStats.Contains(key) && value > 0 ? value : null;
        var items = featured.Select(i =>
        {
            var n = numbers.GetValueOrDefault(i.Id) ?? new ShowcaseNumbers(0, 0, 0, 0);
            return new ShowcaseInstitution(
                i.Name, i.Slug, i.LogoUrl, i.PrimaryColorHex, i.FeaturedBlurb, i.OrganizationType,
                MemberPortalLinks.Url(i.Slug, i.CustomDomain, config["MemberPortalBaseDomain"] ?? config["PlatformBaseDomain"]),
                Shown(i, FeaturedStatKeys.Members, n.Members), Shown(i, FeaturedStatKeys.Groups, n.Groups), Shown(i, FeaturedStatKeys.Events, n.Events),
                i.FeaturedStats.Contains(FeaturedStatKeys.Raised) && n.Raised > 0 ? n.Raised : null);
        }).ToList();

        return new ShowcaseResponse(items, new ShowcaseTotals(
            items.Sum(i => i.Members ?? 0), items.Sum(i => i.Groups ?? 0), items.Sum(i => i.Events ?? 0), items.Sum(i => i.Raised ?? 0m)));
    }
}
