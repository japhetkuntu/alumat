using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class InstitutionStaffService(
    IAlumniPgRepository<StaffEntity> adminRepo,
    IInstitutionAuthService authService,
    IInstitutionAuditLogService auditLog,
    ILogger<InstitutionStaffService> logger) : IInstitutionStaffService
{
    public async Task<IApiResponse<PgPagedResult<InstitutionStaffListItem>>> GetInstitutionStaffAsync(InstitutionStaffFilter filter)
    {
        try
        {
            logger.LogInformation("GetAdmins request — filter: {Filter}", filter.Serialize());
            var search = filter.Search is null ? null : string.Join(" ", filter.Search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLower();
            var result = await adminRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                a => (string.IsNullOrEmpty(filter.Role) || a.Role == filter.Role)
                  && (!filter.GraduationYear.HasValue || (a.YearGroups != null && a.YearGroups.Contains(filter.GraduationYear.Value)))
                  && TextSearch.Matches(search, a.FirstName, a.LastName, (a.FirstName + " " + a.LastName), (a.LastName + " " + a.FirstName), a.Email));

            var items = result.Results.Select(a => new InstitutionStaffListItem(
                a.Id,
                a.FirstName,
                a.LastName,
                a.Email,
                a.Phone,
                a.Role,
                a.YearGroups,
                a.CommunityIds,
                a.IsDisabled,
                a.CreatedAt,
                a.LastLoginAt == null));

            return new PgPagedResult<InstitutionStaffListItem>
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
            logger.LogError(e, "Error retrieving admins — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<InstitutionStaffListItem>>("Failed to retrieve admins");
        }
    }

    public async Task<IApiResponse<InstitutionStaffListItem>> CreateInstitutionStaffAsync(CreateInstitutionStaffRequest request, AuthData createdBy)
    {
        try
        {
            logger.LogInformation("CreateAdmin request — email: {Email} by admin {AdminId}", request.Email, createdBy.Id);
            var email = request.Email.Trim().ToLower();
            var existing = await adminRepo.GetOneAsync(a => a.Email == email);
            if (existing is not null)
                return ApiResponseExtensions.ToConflictApiResponse<InstitutionStaffListItem>("An admin with that email already exists");

            var role = StaffRoles.IsValid(request.Role) ? request.Role! : StaffRoles.ScopedAdmin;

            if (role == StaffRoles.ScopedAdmin && request.YearGroups is { Count: > 1 })
                return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionStaffListItem>("A scoped admin can be assigned to only one batch.");

            var admin = new StaffEntity
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                // Nobody types a password for someone else: this random hash can never be used to sign in, and the
                // person is emailed a link to choose their own (the same one "Forgot password" sends).
                Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Role = role,
                YearGroups = role == StaffRoles.ScopedAdmin ? request.YearGroups : null,
                CommunityIds = role == StaffRoles.ScopedAdmin ? request.CommunityIds : null,
                IsDisabled = request.IsDisabled,
                CreatedBy = createdBy.Id,
            };

            await adminRepo.AddAsync(admin);
            await authService.SendPasswordSetupLinkAsync(admin);
            await auditLog.LogAsync(createdBy, "Staff Created", $"{admin.FirstName} {admin.LastName} ({admin.Email}) — {admin.Role}");

            logger.LogInformation("Admin {AdminId} created by {CreatorId}", admin.Id, createdBy.Id);
            var listItem = new InstitutionStaffListItem(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Phone, admin.Role, admin.YearGroups, admin.CommunityIds, admin.IsDisabled, admin.CreatedAt, true);
            return listItem.ToCreatedApiResponse("Admin added. We emailed them a link to choose their password.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating admin — email: {Email} by admin {AdminId}", request.Email, createdBy.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionStaffListItem>("Failed to create admin");
        }
    }

    public async Task<IApiResponse<object>> ResendInviteAsync(string adminId, AuthData actor)
    {
        try
        {
            var admin = await adminRepo.GetByIdAsync(adminId);
            if (admin is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Admin not found");
            if (admin.IsDisabled)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("This admin is disabled. Enable them before sending a new link.");
            if (admin.LastLoginAt is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("They have already signed in. If they forgot their password, they can use Forgot password on the sign-in page.");

            await authService.SendPasswordSetupLinkAsync(admin);
            await auditLog.LogAsync(actor, "Staff Invite Resent", $"{admin.FirstName} {admin.LastName} ({admin.Email})");
            return new object().ToOkApiResponse("A new link has been emailed.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error resending invite for admin {AdminId}", adminId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to send the link");
        }
    }

    public async Task<IApiResponse<InstitutionStaffListItem>> UpdateInstitutionStaffAsync(string adminId, UpdateInstitutionStaffRequest request, AuthData updatedBy)
    {
        try
        {
            logger.LogInformation("UpdateAdmin request for adminId: {AdminId} by admin {UpdaterId}", adminId, updatedBy.Id);
            var admin = await adminRepo.GetByIdAsync(adminId);
            if (admin is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionStaffListItem>("Admin not found");

            var role = StaffRoles.IsValid(request.Role) ? request.Role! : StaffRoles.ScopedAdmin;

            if (role == StaffRoles.ScopedAdmin && request.YearGroups is { Count: > 1 })
                return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionStaffListItem>("A scoped admin can be assigned to only one batch.");

            var previousRole = admin.Role;
            var previousDisabled = admin.IsDisabled;

            admin.FirstName = request.FirstName.Trim();
            admin.LastName = request.LastName.Trim();
            admin.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            admin.Role = role;
            admin.YearGroups = admin.Role == StaffRoles.ScopedAdmin ? request.YearGroups : null;
            admin.CommunityIds = admin.Role == StaffRoles.ScopedAdmin ? request.CommunityIds : null;
            admin.IsDisabled = request.IsDisabled;
            admin.UpdatedAt = DateTime.UtcNow;
            admin.UpdatedBy = updatedBy.Id;

            await adminRepo.UpdateAsync(admin);

            // Only log the specific things that actually changed, in plain
            // language — a SuperAdmin reviewing this wants to know "what
            // changed," not just "someone hit save."
            if (previousRole != admin.Role)
                await auditLog.LogAsync(updatedBy, "Staff Role Changed", $"{admin.FirstName} {admin.LastName} ({admin.Email}): {previousRole} → {admin.Role}");
            if (previousDisabled != admin.IsDisabled)
                await auditLog.LogAsync(updatedBy, admin.IsDisabled ? "Staff Disabled" : "Staff Re-enabled", $"{admin.FirstName} {admin.LastName} ({admin.Email})");

            var listItem = new InstitutionStaffListItem(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Phone, admin.Role, admin.YearGroups, admin.CommunityIds, admin.IsDisabled, admin.CreatedAt, admin.LastLoginAt == null);
            return listItem.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating admin {AdminId} by admin {UpdaterId}", adminId, updatedBy.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionStaffListItem>("Failed to update admin");
        }
    }
}
