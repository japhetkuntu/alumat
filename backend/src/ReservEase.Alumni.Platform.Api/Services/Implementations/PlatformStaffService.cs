using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class PlatformStaffService(IAlumniPgRepository<PlatformStaff> staffRepo, IAuditLogService auditLog, ILogger<PlatformStaffService> logger)
    : IPlatformStaffService
{
    public async Task<IApiResponse<ReservEase.Alumni.PostgresDb.Sdk.Models.PgPagedResult<PlatformStaffResponse>>> GetStaffAsync(
        int page, int pageSize, string? search)
    {
        try
        {
        var paged = await staffRepo.GetPagedAsync(page, pageSize, sortColumn: "Name", sortDir: "asc",
            filter: string.IsNullOrWhiteSpace(search)
                ? null
                : s => TextSearch.Matches(search, s.Name, s.Email));

        var result = new ReservEase.Alumni.PostgresDb.Sdk.Models.PgPagedResult<PlatformStaffResponse>
        {
            PageIndex = paged.PageIndex,
            PageSize = paged.PageSize,
            Count = paged.Count,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            LowerBoundSize = paged.LowerBoundSize,
            UpperBoundSize = paged.UpperBoundSize,
            Results = paged.Results.Select(ToDto),
        };

        return result.ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetStaffAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<ReservEase.Alumni.PostgresDb.Sdk.Models.PgPagedResult<PlatformStaffResponse>>("Failed to getstaff");
        }}

    public async Task<IApiResponse<PlatformStaffResponse>> CreateAsync(CreatePlatformStaffRequest request, string createdBy, string actorName)
    {
        try
        {
        if (!PlatformStaffRoles.IsValid(request.Role))
            return ApiResponseExtensions.ToBadRequestApiResponse<PlatformStaffResponse>(
                $"Invalid role. Must be one of: {string.Join(", ", PlatformStaffRoles.All)}.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await staffRepo.GetOneAsync(s => s.Email == email) is not null)
            return ApiResponseExtensions.ToConflictApiResponse<PlatformStaffResponse>("A staff account with that email already exists");

        var staff = new PlatformStaff
        {
            Name = request.Name,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            Team = request.Team,
            ReportsToStaffId = string.IsNullOrWhiteSpace(request.ReportsToStaffId) ? null : request.ReportsToStaffId,
            CreatedBy = createdBy,
        };

        // Who they report to is checked against the whole team, so a loop or a wrong kind of manager is refused up front.
        var team = new StaffHierarchy((await staffRepo.GetAllAsync(_ => true)).ToList());
        var problem = team.ValidateManager(staff.Id, staff.Role, staff.ReportsToStaffId);
        if (problem is not null)
            return ApiResponseExtensions.ToBadRequestApiResponse<PlatformStaffResponse>(problem);

        await staffRepo.AddAsync(staff);
        logger.LogInformation("Platform staff {StaffId} created by {CreatedBy}", staff.Id, createdBy);
        await auditLog.LogAsync(createdBy, actorName, "invited platform staff", staff.Name);

        return ToDto(staff).ToCreatedApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "CreateAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformStaffResponse>("Failed to create");
        }}

    public async Task<IApiResponse<PlatformStaffResponse>> UpdateAsync(string id, UpdatePlatformStaffRequest request, string updatedBy, string actorName)
    {
        try
        {
        var staff = await staffRepo.GetByIdAsync(id);
        if (staff is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<PlatformStaffResponse>("Staff account not found");

        if (!PlatformStaffRoles.IsValid(request.Role))
            return ApiResponseExtensions.ToBadRequestApiResponse<PlatformStaffResponse>(
                $"Invalid role. Must be one of: {string.Join(", ", PlatformStaffRoles.All)}.");

        // Omitted means unchanged, so an older client that only toggles access never disturbs the reporting line. Someone who is
        // not a representative has no manager, whatever was sent.
        var newManager = request.ReportsToStaffId is null ? staff.ReportsToStaffId
            : string.IsNullOrWhiteSpace(request.ReportsToStaffId) ? null : request.ReportsToStaffId;
        if (request.Role != PlatformStaffRoles.Representative) newManager = null;
        var team = new StaffHierarchy((await staffRepo.GetAllAsync(_ => true)).ToList());

        // Someone who leads a team cannot stop being a representative (or be disabled) until their people have been moved,
        // otherwise they would be left reporting to nobody.
        var hasReports = team.SelfAndDescendants(staff.Id).Count > 1;
        if (hasReports && (request.Role != PlatformStaffRoles.Representative && request.Role != PlatformStaffRoles.SuperAdmin))
            return ApiResponseExtensions.ToBadRequestApiResponse<PlatformStaffResponse>("Move the people who report to them first.");
        if (hasReports && request.IsDisabled && staff.Role == PlatformStaffRoles.Representative)
            return ApiResponseExtensions.ToBadRequestApiResponse<PlatformStaffResponse>("Move the people who report to them first.");

        var problem = team.ValidateManager(staff.Id, request.Role, newManager);
        if (problem is not null)
            return ApiResponseExtensions.ToBadRequestApiResponse<PlatformStaffResponse>(problem);

        staff.Name = request.Name;
        staff.Role = request.Role;
        staff.Team = request.Team;
        staff.ReportsToStaffId = newManager;
        staff.IsDisabled = request.IsDisabled;
        staff.UpdatedAt = DateTime.UtcNow;
        staff.UpdatedBy = updatedBy;

        await staffRepo.UpdateAsync(staff);
        await auditLog.LogAsync(updatedBy, actorName, request.IsDisabled ? "disabled platform staff" : "updated platform staff", staff.Name);

        return ToDto(staff).ToOkApiResponse("Staff account updated");

        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformStaffResponse>("Failed to update");
        }}

    private static PlatformStaffResponse ToDto(PlatformStaff s) =>
        new(s.Id, s.Name, s.Email, s.Role, s.Team, s.Mfa, s.IsDisabled, s.LastActiveAt, s.ReportsToStaffId);
}
