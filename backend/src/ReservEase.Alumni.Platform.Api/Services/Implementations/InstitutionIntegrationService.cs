using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Integrations;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Platform staff set up an institution's partner integration here: the settings behind its registration links and banner, and the
/// API keys the partner's server calls with. The institution itself never sees or changes any of it.
/// </summary>
public class InstitutionIntegrationService(
    IAlumniPgRepository<InstitutionIntegration> settingsRepo,
    IAlumniPgRepository<IntegrationApiKey> keyRepo,
    IAlumniPgRepository<OnboardingLink> linkRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAuditLogService auditLog,
    ILogger<InstitutionIntegrationService> logger) : IInstitutionIntegrationService
{
    public const int MaxHours = 24 * 30;
    public const int MaxCacheDays = 365;

    public async Task<IApiResponse<IntegrationSettingsResponse>> GetAsync(string institutionId)
    {
        try
        {
            if (await institutionRepo.GetByIdAsync(institutionId, ignoreQueryFilters: true) is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<IntegrationSettingsResponse>("Institution not found");
            return (await BuildAsync(institutionId)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Loading integration for {InstitutionId} failed", institutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntegrationSettingsResponse>("Failed to load the integration");
        }
    }

    public async Task<IApiResponse<IntegrationSettingsResponse>> UpdateAsync(string institutionId, UpdateIntegrationRequest request, string actorId, string actorName)
    {
        try
        {
            var institution = await institutionRepo.GetByIdAsync(institutionId, ignoreQueryFilters: true);
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<IntegrationSettingsResponse>("Institution not found");

            var name = request.PortalName?.Trim() ?? string.Empty;
            var url = request.RedirectUrl?.Trim() ?? string.Empty;
            var message = request.BannerMessage?.Trim() ?? string.Empty;

            if (name.Length > 80) return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationSettingsResponse>("The portal name can be at most 80 characters.");
            if (message.Length > 300) return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationSettingsResponse>("The banner message can be at most 300 characters.");
            if (url.Length > 0 && !IsHttpsUrl(url)) return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationSettingsResponse>("The redirect URL must be a full https:// address.");
            if (request.BannerHours is < 1 or > MaxHours) return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationSettingsResponse>($"The banner duration must be between 1 and {MaxHours} hours.");
            if (request.LinkExpiryHours is < 1 or > MaxHours) return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationSettingsResponse>($"The link expiry must be between 1 and {MaxHours} hours.");
            if (request.StatusCacheDays is < 1 or > MaxCacheDays) return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationSettingsResponse>($"The status cache must be between 1 and {MaxCacheDays} days.");
            if (request.Enabled && (name.Length == 0 || url.Length == 0 || message.Length == 0))
                return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationSettingsResponse>("Set the portal name, redirect URL and banner message before turning the integration on.");

            var settings = await settingsRepo.GetOneAsync(s => s.InstitutionId == institutionId, ignoreQueryFilters: true);
            var isNew = settings is null;
            settings ??= new InstitutionIntegration { InstitutionId = institutionId, CreatedBy = actorId };
            settings.Enabled = request.Enabled;
            settings.PortalName = name;
            settings.RedirectUrl = url;
            settings.BannerMessage = message;
            settings.BannerHours = request.BannerHours;
            settings.LinkExpiryHours = request.LinkExpiryHours;
            settings.StatusCacheDays = request.StatusCacheDays;
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedBy = actorId;
            if (isNew) await settingsRepo.AddAsync(settings); else await settingsRepo.UpdateAsync(settings);

            await auditLog.LogAsync(actorId, actorName, request.Enabled ? "saved integration settings (on)" : "saved integration settings (off)", institution.Name);
            return (await BuildAsync(institutionId)).ToOkApiResponse("Integration saved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Saving integration for {InstitutionId} failed", institutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntegrationSettingsResponse>("Failed to save the integration");
        }
    }

    public async Task<IApiResponse<CreatedIntegrationKeyResponse>> CreateKeyAsync(string institutionId, CreateIntegrationKeyRequest request, string actorId, string actorName)
    {
        try
        {
            var institution = await institutionRepo.GetByIdAsync(institutionId, ignoreQueryFilters: true);
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<CreatedIntegrationKeyResponse>("Institution not found");

            var name = request.Name?.Trim() ?? string.Empty;
            if (name.Length is 0 or > 80) return ApiResponseExtensions.ToBadRequestApiResponse<CreatedIntegrationKeyResponse>("Give the key a name of up to 80 characters, such as the system that will use it.");

            // Saving a key needs a settings row to join against, so one exists (switched off) as soon as the first key does.
            if (await settingsRepo.GetOneAsync(s => s.InstitutionId == institutionId, ignoreQueryFilters: true) is null)
                await settingsRepo.AddAsync(new InstitutionIntegration { InstitutionId = institutionId, CreatedBy = actorId });

            var (key, prefix, hash) = IntegrationSecrets.NewApiKey();
            var row = new IntegrationApiKey { InstitutionId = institutionId, Name = name, KeyPrefix = prefix, KeyHash = hash, CreatedBy = actorId };
            await keyRepo.AddAsync(row);

            await auditLog.LogAsync(actorId, actorName, $"created integration API key \"{name}\"", institution.Name);
            return new CreatedIntegrationKeyResponse(ToItem(row), key).ToCreatedApiResponse("Copy the key now. It will not be shown again.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Creating integration key for {InstitutionId} failed", institutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<CreatedIntegrationKeyResponse>("Failed to create the key");
        }
    }

    public async Task<IApiResponse<object>> RevokeKeyAsync(string institutionId, string keyId, string actorId, string actorName)
    {
        try
        {
            var key = await keyRepo.GetOneAsync(k => k.Id == keyId && k.InstitutionId == institutionId, ignoreQueryFilters: true);
            if (key is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Key not found");
            if (key.RevokedAt is not null) return new object().ToOkApiResponse("Key already revoked");

            key.RevokedAt = DateTime.UtcNow;
            key.UpdatedAt = DateTime.UtcNow;
            key.UpdatedBy = actorId;
            await keyRepo.UpdateAsync(key);

            var institution = await institutionRepo.GetByIdAsync(institutionId, ignoreQueryFilters: true);
            await auditLog.LogAsync(actorId, actorName, $"revoked integration API key \"{key.Name}\"", institution?.Name ?? institutionId);
            return new object().ToOkApiResponse("Key revoked");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Revoking integration key {KeyId} failed", keyId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to revoke the key");
        }
    }

    private async Task<IntegrationSettingsResponse> BuildAsync(string institutionId)
    {
        var settings = await settingsRepo.GetOneAsync(s => s.InstitutionId == institutionId, ignoreQueryFilters: true) ?? new InstitutionIntegration { InstitutionId = institutionId };
        var keys = await keyRepo.GetQueryable(k => k.InstitutionId == institutionId, ignoreQueryFilters: true).OrderByDescending(k => k.CreatedAt).ToListAsync();
        var linksIssued = await linkRepo.CountAsync(l => l.InstitutionId == institutionId, ignoreQueryFilters: true);
        var linksUsed = await linkRepo.CountAsync(l => l.InstitutionId == institutionId && l.ConsumedAt != null, ignoreQueryFilters: true);
        return new IntegrationSettingsResponse(
            settings.Enabled, settings.PortalName, settings.RedirectUrl, settings.BannerMessage, settings.BannerHours, settings.LinkExpiryHours, settings.StatusCacheDays,
            keys.Select(ToItem).ToList(), linksIssued, linksUsed);
    }

    private static IntegrationKeyItem ToItem(IntegrationApiKey k) => new(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.LastUsedAt, k.RevokedAt);

    private static bool IsHttpsUrl(string value) =>
        value.Length <= 500 && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);
}
