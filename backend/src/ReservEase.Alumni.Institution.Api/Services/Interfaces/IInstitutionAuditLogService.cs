using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IInstitutionAuditLogService
{
    Task<IApiResponse<PgPagedResult<InstitutionAuditLogEntryResponse>>> GetEntriesAsync(int page, int pageSize, string? search);
    Task LogAsync(AuthData actor, string action, string target);
}
