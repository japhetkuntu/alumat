using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>Mirrors Platform.Api's own AuditLogService — same shape, tenant-scoped instead of global. See InstitutionAuditLogEntry for what gets logged and why.</summary>
public class InstitutionAuditLogService(IAlumniPgRepository<InstitutionAuditLogEntry> auditLogRepo,
    ILogger<InstitutionAuditLogService> logger) : IInstitutionAuditLogService
{
    public async Task<IApiResponse<PgPagedResult<InstitutionAuditLogEntryResponse>>> GetEntriesAsync(int page, int pageSize, string? search)
    {
        try
        {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var query = auditLogRepo.GetQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search;
            query = query.WhereMatches(a => TextSearch.Matches(s, a.Actor, a.Action, a.Target));
        }

        var totalCount = await query.CountAsync();
        var entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = entries.Select(a => new InstitutionAuditLogEntryResponse(a.Id, a.Actor, a.Action, a.Target, a.CreatedAt)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new PgPagedResult<InstitutionAuditLogEntryResponse>
        {
            PageIndex = page,
            PageSize = pageSize,
            Count = items.Count,
            TotalCount = totalCount,
            TotalPages = totalPages,
            LowerBoundSize = items.Count == 0 ? 0 : ((page - 1) * pageSize) + 1,
            UpperBoundSize = Math.Min(page * pageSize, totalCount),
            Results = items,
        };

        return result.ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetEntriesAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<InstitutionAuditLogEntryResponse>>("Failed to getentries");
        }}

    public async Task LogAsync(AuthData actor, string action, string target)
    {
        await auditLogRepo.AddAsync(new InstitutionAuditLogEntry
        {
            ActorId = actor.Id,
            Actor = actor.Name,
            Action = action,
            Target = target,
            CreatedBy = actor.Id,
        });
    }
}
