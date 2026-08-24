using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IAuditLogService
{
    Task<IApiResponse<PgPagedResult<AuditLogEntryResponse>>> GetEntriesAsync(int page, int pageSize, string? search);
    Task LogAsync(string? actorId, string actorName, string action, string target);
}
