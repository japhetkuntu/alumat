using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public interface ISegmentService
{
    Task<IApiResponse<List<SegmentDto>>> ListAsync();
    Task<IApiResponse<SegmentDto>> CreateAsync(SaveSegmentRequest request, AuthData admin);
    Task<IApiResponse<SegmentDto>> UpdateAsync(string id, SaveSegmentRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteAsync(string id, AuthData admin);
    Task<IApiResponse<SegmentPreview>> PreviewAsync(SegmentCriteria criteria, AuthData admin);
}

/// <summary>Saved audiences. The rule is stored, never a list of people, so it resolves against members as they are now.</summary>
public class SegmentService(
    IAlumniPgRepository<AudienceSegment> segmentRepo,
    AudienceSegmentResolver resolver,
    IInstitutionAuditLogService auditLog,
    ILogger<SegmentService> logger) : ISegmentService
{
    private static SegmentDto ToDto(AudienceSegment s) => new(s.Id, s.Name, s.Description, s.Criteria, s.CreatedAt);

    public async Task<IApiResponse<List<SegmentDto>>> ListAsync()
    {
        try
        {
            var all = await segmentRepo.GetAllAsync(_ => true);
            return all.OrderBy(s => s.Name).Select(ToDto).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing segments");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<SegmentDto>>("Failed to load segments");
        }
    }

    public async Task<IApiResponse<SegmentDto>> CreateAsync(SaveSegmentRequest request, AuthData admin)
    {
        var problem = Validate(request);
        if (problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<SegmentDto>(problem);
        try
        {
            var segment = new AudienceSegment
            {
                Name = request.Name.Trim(), Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Criteria = Clean(request.Criteria), CreatedBy = admin.Id,
            };
            await segmentRepo.AddAsync(segment);
            await auditLog.LogAsync(admin, "Segment Created", segment.Name);
            return ToDto(segment).ToCreatedApiResponse("Segment saved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating segment");
            return ApiResponseExtensions.ToServerErrorApiResponse<SegmentDto>("Failed to save the segment");
        }
    }

    public async Task<IApiResponse<SegmentDto>> UpdateAsync(string id, SaveSegmentRequest request, AuthData admin)
    {
        var problem = Validate(request);
        if (problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<SegmentDto>(problem);
        try
        {
            var segment = await segmentRepo.GetByIdAsync(id);
            if (segment is null) return ApiResponseExtensions.ToNotFoundApiResponse<SegmentDto>("Segment not found");
            segment.Name = request.Name.Trim();
            segment.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            segment.Criteria = Clean(request.Criteria);
            segment.UpdatedAt = DateTime.UtcNow;
            segment.UpdatedBy = admin.Id;
            await segmentRepo.UpdateAsync(segment);
            await auditLog.LogAsync(admin, "Segment Updated", segment.Name);
            return ToDto(segment).ToOkApiResponse("Segment updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating segment {SegmentId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<SegmentDto>("Failed to update the segment");
        }
    }

    public async Task<IApiResponse<object>> DeleteAsync(string id, AuthData admin)
    {
        try
        {
            var segment = await segmentRepo.GetByIdAsync(id);
            if (segment is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Segment not found");
            await segmentRepo.RemoveAsync(segment);
            await auditLog.LogAsync(admin, "Segment Deleted", segment.Name);
            return new object().ToOkApiResponse("Segment deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting segment {SegmentId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete the segment");
        }
    }

    public async Task<IApiResponse<SegmentPreview>> PreviewAsync(SegmentCriteria criteria, AuthData admin)
    {
        if (!SegmentDuesStatuses.IsValid(criteria.DuesStatus))
            return ApiResponseExtensions.ToBadRequestApiResponse<SegmentPreview>("Dues status must be Paid or Unpaid.");
        try
        {
            var (count, members) = await resolver.CountAsync(Clean(criteria), ScopeFor(admin));
            var sample = members.Select(m => new SegmentPreviewMember(m.Id, $"{m.FirstName} {m.LastName}".Trim(), m.Email)).ToList();
            return new SegmentPreview(count, sample).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error previewing segment");
            return ApiResponseExtensions.ToServerErrorApiResponse<SegmentPreview>("Failed to count the audience");
        }
    }

    public static SegmentScope ScopeFor(AuthData admin) => admin.Role != StaffRoles.ScopedAdmin
        ? SegmentScope.Everyone
        : new SegmentScope(false, admin.YearGroups ?? [], admin.CommunityIds ?? []);

    private static string? Validate(SaveSegmentRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Give the segment a name.";
        if (!SegmentDuesStatuses.IsValid(r.Criteria.DuesStatus)) return "Dues status must be Paid or Unpaid.";
        if (r.Criteria.GraduationYearFrom > r.Criteria.GraduationYearTo) return "The first year must not be after the last.";
        return null;
    }

    /// <summary>Drops blanks and duplicates so a saved rule is tidy and compares sensibly.</summary>
    private static SegmentCriteria Clean(SegmentCriteria c) => new()
    {
        Status = string.IsNullOrWhiteSpace(c.Status) ? null : c.Status.Trim(),
        GroupIds = c.GroupIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(),
        GroupKinds = c.GroupKinds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        GraduationYearFrom = c.GraduationYearFrom,
        GraduationYearTo = c.GraduationYearTo,
        DepartmentId = string.IsNullOrWhiteSpace(c.DepartmentId) ? null : c.DepartmentId,
        DuesStatus = string.IsNullOrWhiteSpace(c.DuesStatus) ? null : c.DuesStatus,
        AnswerContains = string.IsNullOrWhiteSpace(c.AnswerContains) ? null : c.AnswerContains.Trim(),
        DormantOnly = c.DormantOnly,
        JoinedWithinDays = c.JoinedWithinDays is > 0 ? c.JoinedWithinDays : null,
    };
}
