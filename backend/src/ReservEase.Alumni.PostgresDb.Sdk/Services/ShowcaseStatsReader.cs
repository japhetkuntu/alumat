using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>What an institution has actually done on AlumUnion, counted from its own data. The website only ever shows these, never typed-in numbers.</summary>
public record ShowcaseNumbers(int Members, int Groups, int Events, decimal Raised);

/// <summary>
/// Counts the numbers behind "featured institutions" for many institutions at once (a handful of grouped queries, however many there
/// are). Used by the platform portal, to preview what will be shown, and by the public website, to show it, so the two can never differ.
/// </summary>
public class ShowcaseStatsReader(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Contribution> contributionRepo)
{
    public async Task<Dictionary<string, ShowcaseNumbers>> ReadAsync(IReadOnlyCollection<string> institutionIds)
    {
        if (institutionIds.Count == 0) return [];
        var ids = institutionIds.ToList();
        var now = DateTime.UtcNow;

        var members = await memberRepo.GetQueryable(m => ids.Contains(m.InstitutionId) && m.Status == "Active", ignoreQueryFilters: true)
            .GroupBy(m => m.InstitutionId).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N);
        var communities = await communityRepo.GetQueryable(c => ids.Contains(c.InstitutionId) && c.IsActive, ignoreQueryFilters: true)
            .GroupBy(c => c.InstitutionId).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N);
        var yearGroups = await batchRepo.GetQueryable(b => ids.Contains(b.InstitutionId) && b.IsActive, ignoreQueryFilters: true)
            .GroupBy(b => b.InstitutionId).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N);
        // Events that have already happened and were not called off: what was actually held.
        var events = await eventRepo.GetQueryable(e => ids.Contains(e.InstitutionId) && e.Status != "Cancelled" && (e.EndDate ?? e.StartDate) < now, ignoreQueryFilters: true)
            .GroupBy(e => e.InstitutionId).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N);
        var raised = await contributionRepo.GetQueryable(c => ids.Contains(c.InstitutionId) && c.Status == "Successful", ignoreQueryFilters: true)
            .GroupBy(c => c.InstitutionId).Select(g => new { Id = g.Key, Sum = g.Sum(c => c.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Sum);

        return ids.ToDictionary(id => id, id => new ShowcaseNumbers(
            members.GetValueOrDefault(id), communities.GetValueOrDefault(id) + yearGroups.GetValueOrDefault(id), events.GetValueOrDefault(id), raised.GetValueOrDefault(id)));
    }
}
