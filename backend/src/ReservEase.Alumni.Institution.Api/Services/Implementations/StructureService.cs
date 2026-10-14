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

            var staff = (await staffRepo.GetAllAsync()).ToList();
            var batches = (await batchRepo.GetAllAsync()).OrderByDescending(b => b.Year).ToList();
            var communities = (await communityRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();
            var memberships = (await membershipRepo.GetAllAsync()).ToList();

            var activeMembers = (await memberRepo.GetAllAsync(m => m.Status == "Active")).ToList();
            var byYear = activeMembers.GroupBy(m => m.GraduationYear).ToDictionary(g => g.Key, g => g.Count());
            var knownYears = batches.Select(b => b.Year).ToHashSet();
            var withoutGroup = activeMembers.Count(m => !knownYears.Contains(m.GraduationYear));
            var missingYears = activeMembers.Where(m => m.GraduationYear > 0 && !knownYears.Contains(m.GraduationYear))
                .Select(m => m.GraduationYear).Distinct().OrderByDescending(y => y).ToList();

            var admins = staff
                .OrderBy(s => s.Role == "SuperAdmin" ? 0 : 1).ThenBy(s => s.FirstName).ThenBy(s => s.LastName)
                .Select(s => new StructureAdminDto(s.Id, s.FirstName, s.LastName, $"{s.FirstName} {s.LastName}".Trim(), s.Email, s.Phone, s.Role,
                    s.IsDisabled, s.YearGroups ?? [], s.CommunityIds ?? [], s.LastLoginAt))
                .ToList();

            var cohorts = batches.Select(b => new StructureCohortDto(b.Id, b.Name, b.Year, b.IsActive, byYear.GetValueOrDefault(b.Year))).ToList();

            var leaderIds = memberships.Where(m => m.Status == "Approved" && m.Role == "Leader").Select(m => m.MemberId).Distinct().ToList();
            var leaderNames = leaderIds.Count == 0 ? [] : (await memberRepo.GetAllAsync(m => leaderIds.Contains(m.Id)))
                .ToDictionary(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim());

            var communityDtos = communities.Select(c =>
            {
                var mine = memberships.Where(m => m.CommunityId == c.Id).ToList();
                return new StructureCommunityDto(c.Id, c.Name, c.IsActive,
                    mine.Count(m => m.Status == "Approved"), mine.Count(m => m.Status == "Pending"),
                    mine.Where(m => m.Status == "Approved" && m.Role == "Leader")
                        .Select(m => new StructureLeaderDto(m.MemberId, leaderNames.GetValueOrDefault(m.MemberId) ?? "Member")).ToList());
            }).ToList();

            // Only live scoped administrators count towards "managed": a disabled account looks after nothing.
            var live = admins.Where(a => a.Role == "ScopedAdmin" && !a.IsDisabled).ToList();
            var withoutScope = live.Where(a => a.YearGroups.Count == 0 && a.CommunityIds.Count == 0).Select(a => a.Id).ToList();
            var unmanagedCohorts = cohorts.Where(c => !live.Any(a => a.YearGroups.Contains(c.Year))).Select(c => c.Id).ToList();
            var unmanagedCommunities = communityDtos.Where(c => !live.Any(a => a.CommunityIds.Contains(c.Id))).Select(c => c.Id).ToList();

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
