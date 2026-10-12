using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class PlatformSettingsService(IAlumniPgRepository<PlatformSettings> platformSettingsRepo, IAuditLogService auditLog,
    IConfiguration configuration, ILogger<PlatformSettingsService> logger) : IPlatformSettingsService
{
    private PlatformSettingsResponse ToResponse(PlatformSettings s) => new(
        s.BlockOverdueCampaignPayments, s.WhatsAppBotEnabled, configuration.GetValue("WaSenderConfig:Enabled", false));

    /// <summary>Fetches the one settings row, creating it with all-defaults on first access rather than requiring a seed migration.</summary>
    private async Task<PlatformSettings> GetOrCreateAsync()
    {
        var settings = await platformSettingsRepo.GetOneAsync(s => s.Id == PlatformSettings.SingletonId);
        if (settings is not null) return settings;

        settings = new PlatformSettings { Id = PlatformSettings.SingletonId };
        await platformSettingsRepo.AddAsync(settings);
        return settings;
    }

    public async Task<IApiResponse<PlatformSettingsResponse>> GetAsync()
    {
        try
        {
        var settings = await GetOrCreateAsync();
        return ToResponse(settings).ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformSettingsResponse>("Failed to get");
        }}

    public async Task<IApiResponse<PlatformSettingsResponse>> UpdateAsync(UpdatePlatformSettingsRequest request, string updatedBy, string actorName)
    {
        try
        {
        var settings = await GetOrCreateAsync();
        var previousBot = settings.WhatsAppBotEnabled;
        settings.BlockOverdueCampaignPayments = request.BlockOverdueCampaignPayments;
        if (request.WhatsAppBotEnabled is { } bot) settings.WhatsAppBotEnabled = bot;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = updatedBy;
        await platformSettingsRepo.UpdateAsync(settings);

        await auditLog.LogAsync(updatedBy, actorName,
            $"set platform-wide \"block overdue campaign payments\" to {request.BlockOverdueCampaignPayments}", "Platform settings");

        if (settings.WhatsAppBotEnabled != previousBot)
        {
            PlatformWhatsAppSwitch.Forget();
            await auditLog.LogAsync(updatedBy, actorName,
                settings.WhatsAppBotEnabled
                    ? "switched the WhatsApp bot ON for every institution"
                    : "switched the WhatsApp bot OFF for every institution (notifications fall back to SMS and email)",
                "Platform settings");
        }

        return ToResponse(settings).ToOkApiResponse("Settings updated");

        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformSettingsResponse>("Failed to update");
        }}
}
