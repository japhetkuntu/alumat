using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Models;
using AdminEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Admin;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class AdminManagementService(
    IAlumniPgRepository<AdminEntity> adminRepo,
    ILogger<AdminManagementService> logger) : IAdminManagementService
{
    public async Task<IApiResponse<PgPagedResult<AdminListItem>>> GetAdminsAsync(AdminFilter filter)
    {
        try
        {
            logger.LogInformation("GetAdmins request — filter: {Filter}", filter.Serialize());
            var result = await adminRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                a => (string.IsNullOrEmpty(filter.Role) || a.Role == filter.Role)
                  && (!filter.GraduationYear.HasValue || a.YearGroup == filter.GraduationYear)
                  && (string.IsNullOrEmpty(filter.Search)
                      || a.FirstName.Contains(filter.Search)
                      || a.LastName.Contains(filter.Search)
                      || a.Email.Contains(filter.Search)));

            var items = result.Results.Select(a => new AdminListItem(
                a.Id,
                a.FirstName,
                a.LastName,
                a.Email,
                a.Role,
                a.YearGroup,
                a.IsDisabled,
                a.CreatedAt));

            return new PgPagedResult<AdminListItem>
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
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<AdminListItem>>("Failed to retrieve admins");
        }
    }

    public async Task<IApiResponse<AdminListItem>> CreateAdminAsync(CreateAdminRequest request, AuthData createdBy)
    {
        try
        {
            logger.LogInformation("CreateAdmin request — email: {Email} by admin {AdminId}", request.Email, createdBy.Id);
            var email = request.Email.Trim().ToLower();
            var existing = await adminRepo.GetOneAsync(a => a.Email == email);
            if (existing is not null)
                return ApiResponseExtensions.ToConflictApiResponse<AdminListItem>("An admin with that email already exists");

            var role = string.IsNullOrWhiteSpace(request.Role) ? "Admin" : request.Role;
            if (role != "SuperAdmin" && role != "Admin")
                role = "Admin";

            var admin = new AdminEntity
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                YearGroup = request.GraduationYear,
                IsDisabled = request.IsDisabled,
                CreatedBy = createdBy.Id,
            };

            await adminRepo.AddAsync(admin);

            logger.LogInformation("Admin {AdminId} created by {CreatorId}", admin.Id, createdBy.Id);
            var listItem = new AdminListItem(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Role, admin.YearGroup, admin.IsDisabled, admin.CreatedAt);
            return listItem.ToCreatedApiResponse("Admin created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating admin — email: {Email} by admin {AdminId}", request.Email, createdBy.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<AdminListItem>("Failed to create admin");
        }
    }

    public async Task<IApiResponse<AdminListItem>> UpdateAdminAsync(string adminId, UpdateAdminRequest request, AuthData updatedBy)
    {
        try
        {
            logger.LogInformation("UpdateAdmin request for adminId: {AdminId} by admin {UpdaterId}", adminId, updatedBy.Id);
            var admin = await adminRepo.GetByIdAsync(adminId);
            if (admin is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<AdminListItem>("Admin not found");

            admin.FirstName = request.FirstName.Trim();
            admin.LastName = request.LastName.Trim();
            admin.Role = string.IsNullOrWhiteSpace(request.Role) ? "Admin" : request.Role;
            if (admin.Role != "SuperAdmin" && admin.Role != "Admin")
                admin.Role = "Admin";
            admin.YearGroup = request.GraduationYear;
            admin.IsDisabled = request.IsDisabled;
            admin.UpdatedAt = DateTime.UtcNow;
            admin.UpdatedBy = updatedBy.Id;

            await adminRepo.UpdateAsync(admin);

            var listItem = new AdminListItem(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Role, admin.YearGroup, admin.IsDisabled, admin.CreatedAt);
            return listItem.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating admin {AdminId} by admin {UpdaterId}", adminId, updatedBy.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<AdminListItem>("Failed to update admin");
        }
    }
}
