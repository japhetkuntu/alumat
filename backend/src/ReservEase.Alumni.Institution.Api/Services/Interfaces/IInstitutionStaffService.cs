using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IInstitutionStaffService
{
    Task<IApiResponse<PgPagedResult<InstitutionStaffListItem>>> GetInstitutionStaffAsync(InstitutionStaffFilter filter);
    Task<IApiResponse<InstitutionStaffListItem>> CreateInstitutionStaffAsync(CreateInstitutionStaffRequest request, AuthData createdBy);
    Task<IApiResponse<InstitutionStaffListItem>> UpdateInstitutionStaffAsync(string adminId, UpdateInstitutionStaffRequest request, AuthData updatedBy);
}
