using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.Admin.Api.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IAdminManagementService
{
    Task<IApiResponse<PgPagedResult<AdminListItem>>> GetAdminsAsync(AdminFilter filter);
    Task<IApiResponse<AdminListItem>> CreateAdminAsync(CreateAdminRequest request, AuthData createdBy);
    Task<IApiResponse<AdminListItem>> UpdateAdminAsync(string adminId, UpdateAdminRequest request, AuthData updatedBy);
}
