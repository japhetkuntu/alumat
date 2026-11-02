using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Website;
using ReservEase.Alumni.Redis.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class WebsiteService(
    IAlumniPgRepository<InstitutionWebsite> siteRepo,
    IAlumniPgRepository<InstitutionWebsiteVersion> versionRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant,
    IRedisService<PublicContentCacheConfig> publicCache,
    IInstitutionAuditLogService auditLog,
    ILogger<WebsiteService> logger) : IWebsiteService
{
    /// <summary>How many earlier publishes are kept for "put this back".</summary>
    public const int VersionsKept = 10;

    private static WebsiteStatusDto Status(InstitutionWebsite? w) => w is null
        ? new WebsiteStatusDto(false, null, null, 0, 0, false)
        : new WebsiteStatusDto(w.IsLive, w.PublishedAt, w.PublishedBy, w.DraftRevision, w.PublishedRevision, w.PublishedJson is null || w.DraftRevision != w.PublishedRevision);

    private Task ClearPublicCacheAsync()
        => currentTenant.InstitutionId is { } id ? publicCache.RemoveAsync(PublicContentCacheKeys.Site(id)) : Task.CompletedTask;

    private async Task<InstitutionEntity?> GetInstitutionAsync()
        => currentTenant.InstitutionId is { } id ? await institutionRepo.GetByIdAsync(id, ignoreQueryFilters: true) : null;

    public async Task<IApiResponse<WebsiteEditorDto>> GetAsync()
    {
        try
        {
            var institution = await GetInstitutionAsync();
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<WebsiteEditorDto>("Institution not found");
            var site = await siteRepo.GetOneAsync(_ => true);
            JRaw? draft = site is null ? null : new JRaw(site.DraftJson);
            var brand = new WebsiteBrandDto(
                string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName,
                institution.Tagline, institution.PrimaryColorHex, institution.SecondaryColorHex, institution.LogoUrl, institution.Slug);
            return new WebsiteEditorDto(Status(site), draft, brand, institution.DisabledFeatures ?? []).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading the website editor");
            return ApiResponseExtensions.ToServerErrorApiResponse<WebsiteEditorDto>("Failed to load the website");
        }
    }

    public async Task<IApiResponse<WebsiteStatusDto>> SaveDraftAsync(SaveWebsiteDraftRequest request, AuthData admin)
    {
        try
        {
            var institution = await GetInstitutionAsync();
            if (request.Document is null) return ApiResponseExtensions.ToBadRequestApiResponse<WebsiteStatusDto>("There is nothing to save.");
            var result = WebsiteDocumentValidator.Validate(request.Document.ToString(Formatting.None), DisabledSet(institution));
            if (!result.IsValid) return ApiResponseExtensions.ToBadRequestApiResponse<WebsiteStatusDto>(result.Errors[0], result.Errors);

            var site = await siteRepo.GetOneAsync(_ => true);
            if (site is null)
            {
                if (request.BaseRevision != 0) return ApiResponseExtensions.ToConflictApiResponse<WebsiteStatusDto>("This page was changed in another window. Reload to see the latest.");
                site = new InstitutionWebsite { DraftJson = result.Json!, DraftRevision = 1, CreatedBy = admin.Id };
                await siteRepo.AddAsync(site);
            }
            else
            {
                if (request.BaseRevision != site.DraftRevision)
                    return ApiResponseExtensions.ToConflictApiResponse<WebsiteStatusDto>("This page was changed in another window. Reload to see the latest.");
                site.DraftJson = result.Json!;
                site.DraftRevision++;
                site.UpdatedBy = admin.Id;
                site.UpdatedAt = DateTime.UtcNow;
                await siteRepo.UpdateAsync(site);
            }
            return Status(site).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error saving the website draft");
            return ApiResponseExtensions.ToServerErrorApiResponse<WebsiteStatusDto>("Failed to save the page");
        }
    }

    public async Task<IApiResponse<WebsiteStatusDto>> PublishAsync(PublishWebsiteRequest request, AuthData admin)
    {
        try
        {
            var site = await siteRepo.GetOneAsync(_ => true);
            if (site is null) return ApiResponseExtensions.ToNotFoundApiResponse<WebsiteStatusDto>("There is no page to publish yet. Make a change first.");
            if (request.Revision != site.DraftRevision)
                return ApiResponseExtensions.ToConflictApiResponse<WebsiteStatusDto>("This page changed since you opened it. Reload, check it, then publish.");

            // Checked again here, not trusted from the last save: the platform may have switched a feature off since.
            var institution = await GetInstitutionAsync();
            var result = WebsiteDocumentValidator.Validate(site.DraftJson, DisabledSet(institution));
            if (!result.IsValid) return ApiResponseExtensions.ToBadRequestApiResponse<WebsiteStatusDto>(result.Errors[0], result.Errors);
            var problems = WebsiteDocumentValidator.ProblemsBeforePublishing(result.Document!);
            if (problems.Count > 0) return ApiResponseExtensions.ToBadRequestApiResponse<WebsiteStatusDto>(problems[0], problems);

            site.PublishedJson = result.Json;
            site.PublishedRevision = site.DraftRevision;
            site.IsLive = true;
            site.PublishedAt = DateTime.UtcNow;
            site.PublishedBy = admin.Name;
            site.UpdatedBy = admin.Id;
            site.UpdatedAt = DateTime.UtcNow;
            await siteRepo.UpdateAsync(site);

            await versionRepo.AddAsync(new InstitutionWebsiteVersion { Revision = site.PublishedRevision, DocumentJson = result.Json!, PublishedByName = admin.Name ?? "", CreatedBy = admin.Id });
            var old = versionRepo.GetQueryable().OrderByDescending(v => v.CreatedAt).Skip(VersionsKept).ToList();
            foreach (var v in old) await versionRepo.RemoveAsync(v);

            await ClearPublicCacheAsync();
            await auditLog.LogAsync(admin, "Website Published", $"Revision {site.PublishedRevision}");
            return Status(site).ToOkApiResponse("Published");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error publishing the website");
            return ApiResponseExtensions.ToServerErrorApiResponse<WebsiteStatusDto>("Failed to publish the page");
        }
    }

    public async Task<IApiResponse<WebsiteStatusDto>> UnpublishAsync(AuthData admin)
    {
        try
        {
            var site = await siteRepo.GetOneAsync(_ => true);
            if (site is null || !site.IsLive) return Status(site).ToOkApiResponse();
            site.IsLive = false;
            site.UpdatedBy = admin.Id;
            site.UpdatedAt = DateTime.UtcNow;
            await siteRepo.UpdateAsync(site);
            await ClearPublicCacheAsync();
            await auditLog.LogAsync(admin, "Website Unpublished", "Back to the standard landing page");
            return Status(site).ToOkApiResponse("Unpublished");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unpublishing the website");
            return ApiResponseExtensions.ToServerErrorApiResponse<WebsiteStatusDto>("Failed to unpublish the page");
        }
    }

    public async Task<IApiResponse<List<WebsiteVersionDto>>> GetVersionsAsync()
    {
        try
        {
            var versions = versionRepo.GetQueryable().OrderByDescending(v => v.CreatedAt).Take(VersionsKept).ToList();
            return versions.Select(v => new WebsiteVersionDto(v.Id, v.Revision, v.CreatedAt, v.PublishedByName)).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing website versions");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<WebsiteVersionDto>>("Failed to load earlier versions");
        }
    }

    public async Task<IApiResponse<WebsiteStatusDto>> RestoreVersionAsync(string versionId, AuthData admin)
    {
        try
        {
            var version = await versionRepo.GetOneAsync(v => v.Id == versionId);
            var site = await siteRepo.GetOneAsync(_ => true);
            if (version is null || site is null) return ApiResponseExtensions.ToNotFoundApiResponse<WebsiteStatusDto>("That version was not found");
            // Restoring only replaces the draft, so nothing changes for visitors until the admin looks at it and publishes.
            site.DraftJson = version.DocumentJson;
            site.DraftRevision++;
            site.UpdatedBy = admin.Id;
            site.UpdatedAt = DateTime.UtcNow;
            await siteRepo.UpdateAsync(site);
            await auditLog.LogAsync(admin, "Website Version Restored", $"Revision {version.Revision}");
            return Status(site).ToOkApiResponse("Restored to the editor. Publish when you are happy.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error restoring a website version");
            return ApiResponseExtensions.ToServerErrorApiResponse<WebsiteStatusDto>("Failed to restore that version");
        }
    }

    private static ISet<string>? DisabledSet(InstitutionEntity? i) => i?.DisabledFeatures is { Count: > 0 } d ? new HashSet<string>(d) : null;
}
