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
    ILogger<WhatsAppHealthService> logger) : IWhatsAppHealthService
{
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
                institutions.Count(i => i.DisabledFeatures.Contains(InstitutionFeatures.WhatsApp)), adoption).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "WhatsApp health check failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppHealthDto>("Failed to read WhatsApp health");
        }
    }
}
