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
    ILogger<InstitutionStaffService> logger) : IInstitutionStaffService
{
    public async Task<IApiResponse<PgPagedResult<InstitutionStaffListItem>>> GetInstitutionStaffAsync(InstitutionStaffFilter filter)
    {
        try
        {
            logger.LogInformation("GetAdmins request — filter: {Filter}", filter.Serialize());
            var result = await adminRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                a => (string.IsNullOrEmpty(filter.Role) || a.Role == filter.Role)
                  && (!filter.GraduationYear.HasValue || (a.YearGroups != null && a.YearGroups.Contains(filter.GraduationYear.Value)))
                  && (string.IsNullOrEmpty(filter.Search)
                      || a.FirstName.Contains(filter.Search)
                      || a.LastName.Contains(filter.Search)
                      || a.Email.Contains(filter.Search)));

            var items = result.Results.Select(a => new InstitutionStaffListItem(
                a.Id,
                a.FirstName,
                a.LastName,
                a.Email,
                a.Role,
                a.YearGroups,
                a.CommunityIds,
                a.IsDisabled,
                a.CreatedAt));

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

            var role = StaffRoles.IsValid(request.Role) ? request.Role : StaffRoles.Admin;

            var admin = new StaffEntity
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                YearGroups = role == StaffRoles.ScopedAdmin ? request.YearGroups : null,
                CommunityIds = role == StaffRoles.ScopedAdmin ? request.CommunityIds : null,
                IsDisabled = request.IsDisabled,
                CreatedBy = createdBy.Id,
            };

            await adminRepo.AddAsync(admin);

            logger.LogInformation("Admin {AdminId} created by {CreatorId}", admin.Id, createdBy.Id);
            var listItem = new InstitutionStaffListItem(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Role, admin.YearGroups, admin.CommunityIds, admin.IsDisabled, admin.CreatedAt);
            return listItem.ToCreatedApiResponse("Admin created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating admin — email: {Email} by admin {AdminId}", request.Email, createdBy.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionStaffListItem>("Failed to create admin");
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

            admin.FirstName = request.FirstName.Trim();
            admin.LastName = request.LastName.Trim();
            admin.Role = StaffRoles.IsValid(request.Role) ? request.Role : StaffRoles.Admin;
            admin.YearGroups = admin.Role == StaffRoles.ScopedAdmin ? request.YearGroups : null;
            admin.CommunityIds = admin.Role == StaffRoles.ScopedAdmin ? request.CommunityIds : null;
            admin.IsDisabled = request.IsDisabled;
            admin.UpdatedAt = DateTime.UtcNow;
            admin.UpdatedBy = updatedBy.Id;

            await adminRepo.UpdateAsync(admin);

            var listItem = new InstitutionStaffListItem(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Role, admin.YearGroups, admin.CommunityIds, admin.IsDisabled, admin.CreatedAt);
            return listItem.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating admin {AdminId} by admin {UpdaterId}", adminId, updatedBy.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionStaffListItem>("Failed to update admin");
        }
    }
}
