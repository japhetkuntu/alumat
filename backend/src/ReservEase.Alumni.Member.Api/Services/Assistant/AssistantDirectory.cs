using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>
/// Cross-institution lookups for the assistant. There is no tenant on a WhatsApp webhook, so these deliberately ignore the tenant
/// filter and are explicit about what they return: a member row only by exact normalised number and Active status, an
/// institution only if it is Active.
/// </summary>
public class AssistantDirectory(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IConfiguration configuration) : IAssistantDirectory
{
    private string? BaseDomain => configuration["MemberPortalBaseDomain"] ?? configuration["PlatformBaseDomain"];

    private AssistantCommunity ToCommunity(Institution i, MemberEntity? member = null) => new(
        i.Id, i.Slug, string.IsNullOrWhiteSpace(i.PortalName) ? i.Name : i.PortalName!,
        MemberPortalLinks.UrlOrEmpty(i.Slug, i.CustomDomain, BaseDomain),
        member?.Id, member?.PhoneVerifiedAt is not null, member?.FirstName, i.IsWhatsAppAllowed(),
        i.GraduationYearMode != GraduationYearModes.Off);

    public async Task<IReadOnlyList<AssistantCommunity>> FindMemberCommunitiesAsync(string normalizedPhone, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(normalizedPhone)) return [];

        var members = (await memberRepo.GetAllAsync(m => m.PhoneNormalized == normalizedPhone && m.Status == "Active", ignoreQueryFilters: true)).ToList();
        if (members.Count == 0) return [];

        var ids = members.Select(m => m.InstitutionId).Distinct().ToList();
        var institutions = (await institutionRepo.GetAllAsync(i => ids.Contains(i.Id) && i.Status == "Active", ignoreQueryFilters: true)).ToDictionary(i => i.Id);

        // A verified row wins over an unverified one for the same institution.
        return members
            .Where(m => institutions.ContainsKey(m.InstitutionId))
            .OrderByDescending(m => m.PhoneVerifiedAt.HasValue)
            .GroupBy(m => m.InstitutionId)
            .Select(g => ToCommunity(institutions[g.Key], g.First()))
            .OrderBy(c => c.Name)
            .ToList();
    }

    public async Task<AssistantCommunity?> FindByIdAsync(string institutionId, CancellationToken ct = default)
    {
        var i = await institutionRepo.GetOneAsync(x => x.Id == institutionId && x.Status == "Active", ignoreQueryFilters: true);
        return i is null ? null : ToCommunity(i);
    }

    public async Task<AssistantCommunity?> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        var s = slug.Trim().ToLowerInvariant();
        if (s.Length == 0) return null;
        var i = await institutionRepo.GetOneAsync(x => x.Slug == s && x.Status == "Active", ignoreQueryFilters: true);
        return i is null ? null : ToCommunity(i);
    }

    public async Task<IReadOnlyList<AssistantCommunity>> SearchAsync(string text, CancellationToken ct = default)
    {
        var q = text.Trim().ToLowerInvariant();
        if (q.Length < 3) return [];

        var bySlug = await FindBySlugAsync(q, ct);
        if (bySlug is not null)
        {
            // An exact code wins over looser name matches, but only when the assistant is allowed there.
            var row = await institutionRepo.GetOneAsync(x => x.Id == bySlug.InstitutionId, ignoreQueryFilters: true);
            if (row is { } r && r.IsWhatsAppAllowed()) return [bySlug];
        }

        var found = await institutionRepo.GetQueryable(i => i.Status == "Active", ignoreQueryFilters: true)
            .Where(i => i.Name.ToLower().Contains(q) || i.PortalName.ToLower().Contains(q) || i.Slug.Contains(q))
            .OrderBy(i => i.Name).Take(20).ToListAsync(ct);
        // DisabledFeatures is a JSON list, so that part is applied here rather than in the query.
        return found.Where(i => i.IsWhatsAppAllowed()).Take(5).Select(i => ToCommunity(i)).ToList();
    }
}
