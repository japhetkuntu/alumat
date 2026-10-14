using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class WhatsAppHealthService(
    IWhatsAppProvider provider,
    IOptions<WaSenderConfig> options,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IConfiguration configuration,
    IAuditLogService auditLog,
    ILogger<WhatsAppHealthService> logger,
    ReservEase.Alumni.PostgresDb.Sdk.Services.IPlatformWhatsAppSwitch? platformSwitch = null) : IWhatsAppHealthService
{
    public async Task<IApiResponse<List<WhatsAppInstitutionRow>>> ListInstitutionsAsync()
    {
        try
        {
            var institutions = (await institutionRepo.GetAllAsync(i => i.Status == "Active", ignoreQueryFilters: true)).OrderBy(i => i.Name).ToList();
            var counts = (await memberRepo.GetQueryable(m => m.Status == "Active", ignoreQueryFilters: true)
                .GroupBy(m => m.InstitutionId)
                .Select(g => new { Id = g.Key, Active = g.Count(), Connected = g.Count(m => m.PhoneVerifiedAt != null) })
                .ToListAsync()).ToDictionary(x => x.Id);

            return institutions.Select(i =>
            {
                counts.TryGetValue(i.Id, out var c);
                return new WhatsAppInstitutionRow(i.Id, string.IsNullOrWhiteSpace(i.PortalName) ? i.Name : i.PortalName, i.Slug, i.WhatsAppBotEnabled,
                    i.DisabledFeatures.Contains(InstitutionFeatures.WhatsApp), c?.Connected ?? 0, c?.Active ?? 0,
                    i.WhatsAppGroups.Count(g => g.Status == WhatsAppGroupStatuses.Active));
            }).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Listing institutions for WhatsApp failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<WhatsAppInstitutionRow>>("Failed to list institutions");
        }
    }

    public async Task<IApiResponse<WhatsAppInstitutionRow>> SetInstitutionAsync(string institutionId, bool enabled, string actorId, string actorName)
    {
        try
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<WhatsAppInstitutionRow>("Institution not found");

            if (institution.WhatsAppBotEnabled != enabled)
            {
                institution.WhatsAppBotEnabled = enabled;
                institution.UpdatedAt = DateTime.UtcNow;
                institution.UpdatedBy = actorId;
                await institutionRepo.UpdateAsync(institution);
                await auditLog.LogAsync(actorId, actorName,
                    enabled ? "activated WhatsApp for this institution" : "deactivated WhatsApp for this institution (its notifications use SMS and email)",
                    institution.Name);
            }

            var members = await memberRepo.CountAsync(m => m.InstitutionId == institution.Id && m.Status == "Active", ignoreQueryFilters: true);
            var connected = await memberRepo.CountAsync(m => m.InstitutionId == institution.Id && m.Status == "Active" && m.PhoneVerifiedAt != null, ignoreQueryFilters: true);
            return new WhatsAppInstitutionRow(institution.Id, string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName, institution.Slug,
                institution.WhatsAppBotEnabled, institution.DisabledFeatures.Contains(InstitutionFeatures.WhatsApp), connected, members,
                institution.WhatsAppGroups.Count(g => g.Status == WhatsAppGroupStatuses.Active)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Setting WhatsApp for an institution failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppInstitutionRow>("Failed to update the institution");
        }
    }

    private async Task<WhatsAppAdoptionSummary> BuildAdoptionAsync(List<Institution> institutions)
    {
        var keys = Enumerable.Range(0, 6).Select(i => DateTime.UtcNow.AddMonths(-i)).Select(MessagingUsageKeys.For).Reverse().ToList();
        var months = keys.Select(k => new WhatsAppAdoptionMonth(
            k,
            institutions.Sum(i => i.MessagingUsage.TryGetValue(k, out var u) ? u.WhatsAppDelivered : 0),
            institutions.Sum(i => i.MessagingUsage.TryGetValue(k, out var u) ? u.SmsSent : 0))).ToList();

        var thisMonth = MessagingUsageKeys.For(DateTime.UtcNow);
        var members = (await memberRepo.GetQueryable(m => m.Status == "Active", ignoreQueryFilters: true)
            .GroupBy(m => m.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Active = g.Count(), Connected = g.Count(m => m.PhoneVerifiedAt != null) })
            .ToListAsync()).ToDictionary(x => x.InstitutionId);

        var rows = institutions.Select(i =>
        {
            members.TryGetValue(i.Id, out var m);
            i.MessagingUsage.TryGetValue(thisMonth, out var u);
            return new WhatsAppAdoptionInstitution(string.IsNullOrWhiteSpace(i.PortalName) ? i.Name : i.PortalName, m?.Active ?? 0, m?.Connected ?? 0,
                u?.WhatsAppDelivered ?? 0, u?.SmsSent ?? 0);
        }).Where(r => r.Connected > 0 || r.ByWhatsAppThisMonth > 0 || r.BySmsThisMonth > 0)
          .OrderByDescending(r => r.ByWhatsAppThisMonth).ThenByDescending(r => r.Connected).Take(25).ToList();

        var alertsOn = await prefRepo.CountAsync(p => p.WhatsAppAlerts, ignoreQueryFilters: true);

        // An estimate only, and only when the platform has said what an SMS costs it.
        decimal? unit = decimal.TryParse(configuration["WhatsAppAssistant:SmsUnitCost"], System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : null;
        var delivered = months.Sum(m => m.ByWhatsApp);
        return new WhatsAppAdoptionSummary(members.Values.Sum(m => m.Active), alertsOn, months, unit, unit is null ? null : Math.Round(unit.Value * delivered, 2), rows);
    }

    public async Task<IApiResponse<WhatsAppHealthDto>> GetAsync()
    {
        try
        {
            var config = options.Value;
            var apiKey = !string.IsNullOrWhiteSpace(config.ApiKey);

            // Only ask the provider when there is a key to ask with: an unconfigured channel is reported as that, not as an outage.
            var status = apiKey ? await provider.GetConnectionStatusAsync() : null;

            var institutions = (await institutionRepo.GetAllAsync(i => i.Status == "Active", ignoreQueryFilters: true)).ToList();
            var groups = institutions.SelectMany(i => i.WhatsAppGroups).Where(g => g.Status == WhatsAppGroupStatuses.Active).ToList();
            var verified = await memberRepo.CountAsync(m => m.PhoneVerifiedAt != null && m.Status == "Active", ignoreQueryFilters: true);

            var adoption = await BuildAdoptionAsync(institutions);

            return new WhatsAppHealthDto(
                config.Enabled, apiKey, !string.IsNullOrWhiteSpace(config.WebhookSecret), !string.IsNullOrWhiteSpace(config.AssistantNumber),
                status?.Connected ?? false, status?.State ?? (apiKey ? "unknown" : "not configured"),
                institutions.Count(i => i.WhatsAppGroups.Any(g => g.Status == WhatsAppGroupStatuses.Active)), groups.Count, verified,
                institutions.Count(i => i.DisabledFeatures.Contains(InstitutionFeatures.WhatsApp)), adoption,
                platformSwitch is null || await platformSwitch.IsOnAsync()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "WhatsApp health check failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppHealthDto>("Failed to read WhatsApp health");
        }
    }
}
