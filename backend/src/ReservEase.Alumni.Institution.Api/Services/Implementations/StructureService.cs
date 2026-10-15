using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public interface IStructureService
{
    Task<IApiResponse<StructureDto>> GetAsync();
}

/// <summary>
/// Reads how the institution is organised: who the administrators are, the groups and communities, and which administrator is
/// responsible for which. Read only. Changing the structure uses the existing staff, batch and community endpoints, so every rule
/// and audit entry those already have still applies.
/// </summary>
public class StructureService(
    IAlumniPgRepository<PostgresDb.Sdk.Entities.Institution> institutionRepo,
    IAlumniPgRepository<InstitutionStaff> staffRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    ICurrentTenantService tenant,
    ILogger<StructureService> logger) : IStructureService
{
    public async Task<IApiResponse<StructureDto>> GetAsync()
    {
        try
        {
            var institution = tenant.InstitutionId is null ? null
                : await institutionRepo.GetOneAsync(i => i.Id == tenant.InstitutionId, ignoreQueryFilters: true);
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<StructureDto>("Institution not found");

            // Everything below is aggregated in the database and only the columns the screen needs come back. An
            // institution that has run for 80 years has tens of thousands of members and memberships: loading those
            // rows to count them in memory is what made this endpoint heavy.
            var staff = (await staffRepo.GetAllAsync()).ToList();

            var batches = await batchRepo.GetQueryable()
                .OrderByDescending(b => b.Year)
                .Select(b => new { b.Id, b.Name, b.Year, b.IsActive })
                .ToListAsync();
            var communities = await communityRepo.GetQueryable()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.IsActive })
                .ToListAsync();

            var byYear = (await memberRepo.GetQueryable(m => m.Status == "Active")
                    .GroupBy(m => m.GraduationYear)
                    .Select(g => new { Year = g.Key, Count = g.Count() })
                    .ToListAsync())
                .ToDictionary(x => x.Year, x => x.Count);
            var knownYears = batches.Select(b => b.Year).ToHashSet();
            var withoutGroup = byYear.Where(kv => !knownYears.Contains(kv.Key)).Sum(kv => kv.Value);
            var missingYears = byYear.Keys.Where(y => y > 0 && !knownYears.Contains(y)).OrderByDescending(y => y).ToList();

            var admins = staff
                .OrderBy(s => s.Role == "SuperAdmin" ? 0 : 1).ThenBy(s => s.FirstName).ThenBy(s => s.LastName)
                .Select(s => new StructureAdminDto(s.Id, s.FirstName, s.LastName, $"{s.FirstName} {s.LastName}".Trim(), s.Email, s.Phone, s.Role,
                    s.IsDisabled, s.YearGroups ?? [], s.CommunityIds ?? [], s.LastLoginAt))
                .ToList();

            var cohorts = batches.Select(b => new StructureCohortDto(b.Id, b.Name, b.Year, b.IsActive, byYear.GetValueOrDefault(b.Year))).ToList();

            // Member counts per community and status in one grouped query, then the leaders only.
            var membershipCounts = (await membershipRepo.GetQueryable()
                    .GroupBy(m => new { m.CommunityId, m.Status })
                    .Select(g => new { g.Key.CommunityId, g.Key.Status, Count = g.Count() })
                    .ToListAsync())
                .ToDictionary(x => (x.CommunityId, x.Status), x => x.Count);
            var leaderLinks = await membershipRepo.GetQueryable(m => m.Status == "Approved" && m.Role == "Leader")
                .Select(m => new { m.CommunityId, m.MemberId })
                .ToListAsync();
            var leaderIds = leaderLinks.Select(l => l.MemberId).Distinct().ToList();
            var leaderNames = leaderIds.Count == 0
                ? new Dictionary<string, string>()
                : (await memberRepo.GetQueryable(m => leaderIds.Contains(m.Id))
                        .Select(m => new { m.Id, m.FirstName, m.LastName })
                        .ToListAsync())
                    .ToDictionary(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim());
            var leadersByCommunity = leaderLinks.ToLookup(l => l.CommunityId);

            var communityDtos = communities.Select(c => new StructureCommunityDto(
                c.Id, c.Name, c.IsActive,
                membershipCounts.GetValueOrDefault((c.Id, "Approved")), membershipCounts.GetValueOrDefault((c.Id, "Pending")),
                // The screen shows the first couple of leaders; bounding it keeps the payload small however many a group has.
                leadersByCommunity[c.Id].Take(5)
                    .Select(l => new StructureLeaderDto(l.MemberId, leaderNames.GetValueOrDefault(l.MemberId) ?? "Member")).ToList())).ToList();

            // Only live scoped administrators count towards "managed": a disabled account looks after nothing.
            var live = admins.Where(a => a.Role == "ScopedAdmin" && !a.IsDisabled).ToList();
            var withoutScope = live.Where(a => a.YearGroups.Count == 0 && a.CommunityIds.Count == 0).Select(a => a.Id).ToList();
            var managedYears = live.SelectMany(a => a.YearGroups).ToHashSet();
            var managedCommunities = live.SelectMany(a => a.CommunityIds).ToHashSet();
            var unmanagedCohorts = cohorts.Where(c => !managedYears.Contains(c.Year)).Select(c => c.Id).ToList();
            var unmanagedCommunities = communityDtos.Where(c => !managedCommunities.Contains(c.Id)).Select(c => c.Id).ToList();

            return new StructureDto(
                string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName,
                institution.LogoUrl, institution.OrganizationType, institution.CohortLabel, institution.CohortLabelPlural,
                admins, cohorts, communityDtos, withoutGroup, missingYears, withoutScope, unmanagedCohorts, unmanagedCommunities).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Loading the institution structure failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<StructureDto>("Failed to load the structure");
        }
    }
}
