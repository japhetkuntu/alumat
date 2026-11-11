using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class ShowcaseService(
    IAlumniPgRepository<Institution> institutionRepo,
    ShowcaseStatsReader stats,
    IAuditLogService auditLog,
    IConfiguration config,
    ILogger<ShowcaseService> logger) : IShowcaseService
{
    public const int BlurbMaxLength = 160;

    public async Task<IApiResponse<List<ShowcaseEntry>>> GetAsync()
    {
        try
        {
            var institutions = await institutionRepo.GetQueryable(ignoreQueryFilters: true)
                .OrderByDescending(i => i.FeaturedOnWebsite).ThenBy(i => i.FeaturedOrder).ThenBy(i => i.Name).ToListAsync();
            var numbers = await stats.ReadAsync(institutions.Select(i => i.Id).ToList());
            return institutions.Select(i => ToEntry(i, numbers.GetValueOrDefault(i.Id) ?? new ShowcaseNumbers(0, 0, 0, 0))).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Showcase list failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<ShowcaseEntry>>("Failed to load the showcase");
        }
    }

    public async Task<IApiResponse<ShowcaseEntry>> SaveAsync(string institutionId, SaveShowcaseRequest request, string actorId, string actorName)
    {
        try
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<ShowcaseEntry>("Institution not found");

            var blurb = string.IsNullOrWhiteSpace(request.Blurb) ? null : request.Blurb.Trim();
            if (blurb is { Length: > BlurbMaxLength })
                return ApiResponseExtensions.ToBadRequestApiResponse<ShowcaseEntry>($"Keep the description to {BlurbMaxLength} characters.");
            var chosen = (request.Stats ?? []).Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();
            if (chosen.Any(s => !FeaturedStatKeys.All.Contains(s)))
                return ApiResponseExtensions.ToBadRequestApiResponse<ShowcaseEntry>("One of those numbers can't be shown.");

            if (request.Featured)
            {
                if (institution.Status != "Active")
                    return ApiResponseExtensions.ToBadRequestApiResponse<ShowcaseEntry>("Only an active institution can be featured.");
                if (!request.PermissionConfirmed)
                    return ApiResponseExtensions.ToBadRequestApiResponse<ShowcaseEntry>("Confirm the institution has agreed to be shown on the website.");
            }

            var wasFeatured = institution.FeaturedOnWebsite;
            institution.FeaturedOnWebsite = request.Featured;
            institution.FeaturedBlurb = blurb;
            institution.FeaturedOrder = request.Order;
            institution.FeaturedStats = chosen;
            if (request.Featured && !wasFeatured) { institution.FeaturedAt = DateTime.UtcNow; institution.FeaturedBy = actorName; }
            if (!request.Featured) { institution.FeaturedAt = null; institution.FeaturedBy = null; }
            institution.UpdatedAt = DateTime.UtcNow;
            institution.UpdatedBy = actorId;
            await institutionRepo.UpdateAsync(institution);

            if (request.Featured != wasFeatured)
                await auditLog.LogAsync(actorId, actorName, request.Featured ? "featured institution on the website" : "removed institution from the website", institution.Name);

            var numbers = (await stats.ReadAsync([institution.Id])).GetValueOrDefault(institution.Id) ?? new ShowcaseNumbers(0, 0, 0, 0);
            return ToEntry(institution, numbers).ToOkApiResponse("Saved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Showcase save failed for {InstitutionId}", institutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ShowcaseEntry>("Failed to save");
        }
    }

    private ShowcaseEntry ToEntry(Institution i, ShowcaseNumbers numbers) => new(
        i.Id, i.Name, i.Slug, i.LogoUrl, i.Tagline, i.Status, i.OrganizationType,
        MemberPortalLinks.Url(i.Slug, i.CustomDomain, config["PlatformBaseDomain"]),
        i.FeaturedOnWebsite, i.FeaturedBlurb, i.FeaturedOrder, i.FeaturedStats, i.FeaturedAt, i.FeaturedBy, numbers);
}
