using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>A community WhatsApp group a member may join. Only what is needed to show a "Join" button.</summary>
public sealed record JoinableWhatsAppGroup(string Name, string? CommunityId, string? CommunityName, string InviteLink);

/// <summary>
/// The WhatsApp groups this signed-in member may be shown an invitation to: connected by an administrator, with an invite link
/// they chose to share, and either institution-wide or belonging to a community the member is approved in. Nothing about the
/// group's participants is known or shown here.
/// </summary>
public class WhatsAppGroupDirectory(
    ICurrentTenantService tenant,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo)
{
    public async Task<IReadOnlyList<JoinableWhatsAppGroup>> ForMemberAsync(string memberId)
    {
        if (tenant.InstitutionId is null) return [];
        var institution = await institutionRepo.GetOneAsync(i => i.Id == tenant.InstitutionId, ignoreQueryFilters: true);
        if (institution is null || !institution.IsWhatsAppAllowed()) return [];

        var candidates = institution.WhatsAppGroups
            .Where(g => g.Status == WhatsAppGroupStatuses.Active && !string.IsNullOrWhiteSpace(g.InviteLink)).ToList();
        if (candidates.Count == 0) return [];

        var approved = (await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved")).Select(m => m.CommunityId).ToHashSet();
        var visible = candidates.Where(g => g.CommunityId is null || approved.Contains(g.CommunityId)).ToList();

        var names = (await communityRepo.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
        return visible
            .Select(g => new JoinableWhatsAppGroup(g.Name, g.CommunityId, g.CommunityId is not null && names.TryGetValue(g.CommunityId, out var n) ? n : null, g.InviteLink!))
            .ToList();
    }
}
