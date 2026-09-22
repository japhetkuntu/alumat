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
    IAuditLogService auditLog,
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
            var search = filter.Search?.Trim().ToLower();
            var activeCutoff = DateTime.UtcNow.AddDays(-ActiveWindowDays);

            var result = await memberRepo.GetPagedAsync(
                page, pageSize, "CreatedAt", "desc",
                m => (string.IsNullOrEmpty(filter.InstitutionId) || m.InstitutionId == filter.InstitutionId)
                  && (string.IsNullOrEmpty(filter.Status) || m.Status == filter.Status)
                  && (!filter.ActiveOnly.HasValue || (filter.ActiveOnly.Value
                        ? (m.LastLoginAt != null && m.LastLoginAt >= activeCutoff)
                        : (m.LastLoginAt == null || m.LastLoginAt < activeCutoff)))
                  && (string.IsNullOrEmpty(search) ||
                      m.FirstName.ToLower().Contains(search) ||
                      m.LastName.ToLower().Contains(search) ||
                      m.Email.ToLower().Contains(search)),
                ignoreQueryFilters: true);

            var institutionIds = result.Results.Select(m => m.InstitutionId).Distinct().ToList();
            var institutions = await institutionRepo.GetAllAsync(i => institutionIds.Contains(i.Id), ignoreQueryFilters: true);
            var institutionLookup = institutions.ToDictionary(i => i.Id);

            var items = result.Results.Select(m => new PlatformMemberListItem(
                m.Id, m.FirstName, m.LastName, m.Email,
                m.InstitutionId, institutionLookup.GetValueOrDefault(m.InstitutionId)?.Name ?? "Unknown institution",
                institutionLookup.GetValueOrDefault(m.InstitutionId)?.OrganizationType ?? "Community",
                m.GraduationYear, m.Status,
                m.LastLoginAt, m.LastLoginAt != null && m.LastLoginAt >= activeCutoff, m.CreatedAt,
                m.ConnectionType, m.Skills, m.Interests,
                m.ShowEmailOnDirectory, m.ShowPhoneOnDirectory, m.ShowCompanyOnDirectory, m.ShowBioOnDirectory)).ToList();

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

    public async Task<IApiResponse<object>> UpdateMemberProfileAsync(string id, UpdatePlatformMemberProfileRequest request, string actorId, string actorName)
    {
        try
        {
        var member = await memberRepo.GetOneAsync(m => m.Id == id, ignoreQueryFilters: true);
        if (member is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

        member.ConnectionType = request.ConnectionType is null ? member.ConnectionType : string.IsNullOrWhiteSpace(request.ConnectionType) ? null : request.ConnectionType.Trim();
        member.Skills = request.Skills ?? member.Skills;
        member.Interests = request.Interests ?? member.Interests;
        member.ShowEmailOnDirectory = request.ShowEmailOnDirectory ?? member.ShowEmailOnDirectory;
        member.ShowPhoneOnDirectory = request.ShowPhoneOnDirectory ?? member.ShowPhoneOnDirectory;
        member.ShowCompanyOnDirectory = request.ShowCompanyOnDirectory ?? member.ShowCompanyOnDirectory;
        member.ShowBioOnDirectory = request.ShowBioOnDirectory ?? member.ShowBioOnDirectory;
        await memberRepo.UpdateAsync(member);
        await auditLog.LogAsync(actorId, actorName, "updated member community profile and directory visibility", $"{member.FirstName} {member.LastName} ({member.Email})");
        return ((object)new { id = member.Id }).ToOkApiResponse("Member profile updated");

        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateMemberProfileAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to updatememberprofile");
        }}
}
