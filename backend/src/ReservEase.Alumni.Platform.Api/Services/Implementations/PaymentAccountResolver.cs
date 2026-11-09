using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Which account a payment settled into: the institution's own, or a group's or year group's own account once that has been approved.
/// The platform sees every institution, so this works across tenants. It applies the same rule the charge itself uses
/// (see <see cref="SettlementAccounts"/>), so the figures here agree with where the money really went.
/// </summary>
public class PaymentAccountResolver(
    IAlumniPgRepository<Batch> batchRepo, IAlumniPgRepository<Community> communityRepo, IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo, IAlumniPgRepository<ServiceType> serviceTypeRepo)
{
    public const string MainAccount = "Main account";

    public sealed class Accounts
    {
        internal Dictionary<string, string> Groups { get; init; } = [];
        internal Dictionary<(string Institution, int Year), string> Years { get; init; } = [];
        internal Dictionary<string, Campaign> Campaigns { get; init; } = [];
        internal Dictionary<string, AlumniEvent> Events { get; init; } = [];
        internal Dictionary<string, ServiceType> Services { get; init; } = [];

        /// <summary>The name of the group or year group account a payment went into, or null when it went to the institution's own.</summary>
        public string? For(Contribution c)
        {
            if (!Campaigns.TryGetValue(c.CampaignId, out var campaign)) return null;
            return OfAudience(c.InstitutionId, campaign.CommunityId, campaign.YearGroups is { Count: 1 } y ? y[0] : null);
        }

        public string? For(EventRsvp t) => Events.TryGetValue(t.EventId, out var ev) ? OfAudience(t.InstitutionId, ev.CommunityId, null) : null;

        public string? For(ServiceRequest r)
        {
            if (!Services.TryGetValue(r.ServiceTypeId, out var service)) return null;
            var (community, year) = SettlementAccounts.AudienceOfService(service.SettlementTarget, service.CommunityId);
            return OfAudience(r.InstitutionId, community, year);
        }

        // A group takes precedence over a year group, as it does when the payment is charged.
        private string? OfAudience(string institutionId, string? communityId, int? year)
        {
            if (!string.IsNullOrEmpty(communityId)) return Groups.GetValueOrDefault(communityId);
            return year is { } y ? Years.GetValueOrDefault((institutionId, y)) : null;
        }
    }

    /// <summary>Loads what is needed to place the given payments, for one institution or (null) for all of them.</summary>
    public async Task<Accounts> LoadAsync(string? institutionId, IEnumerable<Contribution> contributions, IEnumerable<EventRsvp> tickets, IEnumerable<ServiceRequest> requests)
    {
        var campaignIds = contributions.Select(c => c.CampaignId).Distinct().ToList();
        var eventIds = tickets.Select(t => t.EventId).Distinct().ToList();
        var serviceIds = requests.Select(r => r.ServiceTypeId).Distinct().ToList();

        var campaigns = campaignIds.Count == 0 ? [] : await campaignRepo.GetQueryable(c => campaignIds.Contains(c.Id), ignoreQueryFilters: true).ToDictionaryAsync(c => c.Id);
        var events = eventIds.Count == 0 ? [] : await eventRepo.GetQueryable(e => eventIds.Contains(e.Id), ignoreQueryFilters: true).ToDictionaryAsync(e => e.Id);
        var services = serviceIds.Count == 0 ? [] : await serviceTypeRepo.GetQueryable(s => serviceIds.Contains(s.Id), ignoreQueryFilters: true).ToDictionaryAsync(s => s.Id);

        var groups = (await communityRepo.GetQueryable(g => (institutionId == null || g.InstitutionId == institutionId) && g.PayoutStatus == "Approved" && !g.UseInstitutionAccount && g.PaystackSubaccountCode != null, ignoreQueryFilters: true)
            .Select(g => new { g.Id, g.Name }).ToListAsync()).ToDictionary(g => g.Id, g => g.Name);
        var years = (await batchRepo.GetQueryable(b => (institutionId == null || b.InstitutionId == institutionId) && b.PayoutStatus == "Approved" && !b.UseInstitutionAccount && b.PaystackSubaccountCode != null, ignoreQueryFilters: true)
            .Select(b => new { b.InstitutionId, b.Year, b.Name }).ToListAsync()).GroupBy(b => (b.InstitutionId, b.Year)).ToDictionary(g => g.Key, g => g.First().Name);

        return new Accounts { Groups = groups, Years = years, Campaigns = campaigns, Events = events, Services = services };
    }
}
