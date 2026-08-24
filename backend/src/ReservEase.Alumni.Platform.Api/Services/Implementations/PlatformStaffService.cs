using ReservEase.Alumni.Common.Sdk.Models;
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
        var paged = await staffRepo.GetPagedAsync(page, pageSize, sortColumn: "Name", sortDir: "asc",
            filter: string.IsNullOrWhiteSpace(search)
                ? null
                : s => s.Name.Contains(search) || s.Email.Contains(search));

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

    public async Task<IApiResponse<PlatformStaffResponse>> CreateAsync(CreatePlatformStaffRequest request, string createdBy, string actorName)
    {
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
            CreatedBy = createdBy,
        };

        await staffRepo.AddAsync(staff);
        logger.LogInformation("Platform staff {StaffId} created by {CreatedBy}", staff.Id, createdBy);
        await auditLog.LogAsync(createdBy, actorName, "invited platform staff", staff.Name);

        return ToDto(staff).ToCreatedApiResponse();
    }

    public async Task<IApiResponse<PlatformStaffResponse>> UpdateAsync(string id, UpdatePlatformStaffRequest request, string updatedBy, string actorName)
    {
        var staff = await staffRepo.GetByIdAsync(id);
        if (staff is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<PlatformStaffResponse>("Staff account not found");

        staff.Name = request.Name;
        staff.Role = request.Role;
        staff.Team = request.Team;
        staff.IsDisabled = request.IsDisabled;
        staff.UpdatedAt = DateTime.UtcNow;
        staff.UpdatedBy = updatedBy;

        await staffRepo.UpdateAsync(staff);
        await auditLog.LogAsync(updatedBy, actorName, request.IsDisabled ? "disabled platform staff" : "updated platform staff", staff.Name);

        return ToDto(staff).ToOkApiResponse("Staff account updated");
    }

    private static PlatformStaffResponse ToDto(PlatformStaff s) =>
        new(s.Id, s.Name, s.Email, s.Role, s.Team, s.Mfa, s.IsDisabled, s.LastActiveAt);
}
