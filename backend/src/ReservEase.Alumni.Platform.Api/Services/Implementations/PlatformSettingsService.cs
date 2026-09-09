using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class PlatformSettingsService(AlumniDbContext db, IAuditLogService auditLog) : IPlatformSettingsService
{
    /// <summary>Fetches the one settings row, creating it with all-defaults on first access rather than requiring a seed migration.</summary>
    private async Task<PlatformSettings> GetOrCreateAsync()
    {
        var settings = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId);
        if (settings is not null) return settings;

        settings = new PlatformSettings { Id = PlatformSettings.SingletonId };
        db.PlatformSettings.Add(settings);
        await db.SaveChangesAsync();
        return settings;
    }

    public async Task<IApiResponse<PlatformSettingsResponse>> GetAsync()
    {
        var settings = await GetOrCreateAsync();
        return new PlatformSettingsResponse(settings.BlockOverdueCampaignPayments).ToOkApiResponse();
    }

    public async Task<IApiResponse<PlatformSettingsResponse>> UpdateAsync(UpdatePlatformSettingsRequest request, string updatedBy, string actorName)
    {
        var settings = await GetOrCreateAsync();
        settings.BlockOverdueCampaignPayments = request.BlockOverdueCampaignPayments;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(updatedBy, actorName,
            $"set platform-wide \"block overdue campaign payments\" to {request.BlockOverdueCampaignPayments}", "Platform settings");

        return new PlatformSettingsResponse(settings.BlockOverdueCampaignPayments).ToOkApiResponse("Settings updated");
    }
}
