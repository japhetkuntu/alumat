using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class AuditLogService(AlumniDbContext db) : IAuditLogService
{
    public async Task<IApiResponse<PgPagedResult<AuditLogEntryResponse>>> GetEntriesAsync(int page, int pageSize, string? search)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var query = db.AuditLogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a => a.Actor.ToLower().Contains(s) || a.Action.ToLower().Contains(s) || a.Target.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = entries.Select(a => new AuditLogEntryResponse(a.Id, a.Actor, a.Action, a.Target, a.CreatedAt)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new PgPagedResult<AuditLogEntryResponse>
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

    public async Task LogAsync(string? actorId, string actorName, string action, string target)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            ActorId = actorId,
            Actor = actorName,
            Action = action,
            Target = target,
            CreatedBy = actorId ?? "system",
        });
        await db.SaveChangesAsync();
    }
}
