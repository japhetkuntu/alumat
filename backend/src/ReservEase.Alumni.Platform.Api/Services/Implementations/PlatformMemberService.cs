using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Cross-institution member visibility for platform staff — "who are all the alumni on the
/// platform, and which of them are actually active." Every read here goes ignoreQueryFilters:
/// true, the same way InstitutionManagementService's own member-count rollups do, since
/// platform staff have no single tenant to scope to.
/// </summary>
public class PlatformMemberService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ILogger<PlatformMemberService> logger) : IPlatformMemberService
{
    /// <summary>A member who has signed in within this window counts as "active" on the platform-wide view.</summary>
    private const int ActiveWindowDays = 7;

    public async Task<IApiResponse<PgPagedResult<PlatformMemberListItem>>> GetMembersAsync(PlatformMemberFilter filter)
    {
        try
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize is < 1 or > 200 ? 50 : filter.PageSize;
            var search = filter.Search is null ? null : string.Join(" ", filter.Search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLower();
            var activeCutoff = DateTime.UtcNow.AddDays(-ActiveWindowDays);

            var result = await memberRepo.GetPagedAsync(
                page, pageSize, "CreatedAt", "desc",
                m => (string.IsNullOrEmpty(filter.InstitutionId) || m.InstitutionId == filter.InstitutionId)
                  && (string.IsNullOrEmpty(filter.Status) || m.Status == filter.Status)
                  && (!filter.ActiveOnly.HasValue || (filter.ActiveOnly.Value
                        ? (m.LastLoginAt != null && m.LastLoginAt >= activeCutoff)
                        : (m.LastLoginAt == null || m.LastLoginAt < activeCutoff)))
                  && TextSearch.Matches(search, m.FirstName, m.LastName, (m.FirstName + " " + m.LastName), (m.LastName + " " + m.FirstName), m.Email),
                ignoreQueryFilters: true);

            var institutionIds = result.Results.Select(m => m.InstitutionId).Distinct().ToList();
            var institutions = await institutionRepo.GetAllAsync(i => institutionIds.Contains(i.Id), ignoreQueryFilters: true);
            var institutionLookup = institutions.ToDictionary(i => i.Id);

            var items = result.Results.Select(m => new PlatformMemberListItem(
                m.Id, m.FirstName, m.LastName, m.Email,
                m.InstitutionId, institutionLookup.GetValueOrDefault(m.InstitutionId)?.Name ?? "Unknown institution",
                institutionLookup.GetValueOrDefault(m.InstitutionId)?.OrganizationType ?? "Community",
                m.GraduationYear, m.Status,
                m.LastLoginAt, m.LastLoginAt != null && m.LastLoginAt >= activeCutoff, m.CreatedAt)).ToList();

            return new PgPagedResult<PlatformMemberListItem>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = items,
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving platform-wide members (institutionId={InstitutionId}, status={Status}, search={Search})", filter.InstitutionId, filter.Status, filter.Search);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<PlatformMemberListItem>>("Failed to retrieve members");
        }

    }
}
