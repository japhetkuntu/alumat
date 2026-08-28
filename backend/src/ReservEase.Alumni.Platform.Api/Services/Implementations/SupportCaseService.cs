using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using NotificationEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Notification;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class SupportCaseService(AlumniDbContext db, IAuditLogService auditLog) : ISupportCaseService
{
    public async Task<IApiResponse<List<SupportCaseResponse>>> GetCasesAsync(string? status)
    {
        var query = db.SupportCases.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        var cases = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        var items = await ToDtosAsync(cases);
        return items.ToOkApiResponse();
    }

    public async Task<IApiResponse<SupportCaseResponse>> CreateAsync(CreateSupportCaseRequest request, string createdBy)
    {
        var supportCase = new SupportCase
        {
            InstitutionId = request.InstitutionId,
            Subject = request.Subject,
            Severity = request.Severity,
            Requester = request.Requester,
            RequesterEmail = request.RequesterEmail,
            Message = request.Message,
            CreatedBy = createdBy,
        };
        db.SupportCases.Add(supportCase);
        await db.SaveChangesAsync();

        var dto = (await ToDtosAsync([supportCase])).Single();
        return dto.ToCreatedApiResponse();
    }

    public async Task<IApiResponse<SupportCaseResponse>> UpdateStatusAsync(string id, UpdateSupportCaseStatusRequest request, string actorId, string actorName)
    {
        var supportCase = await db.SupportCases.FirstOrDefaultAsync(c => c.Id == id);
        if (supportCase is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<SupportCaseResponse>("Support case not found");

        var wasAlreadyResolved = supportCase.Status == "Resolved";
        supportCase.Status = request.Status;
        supportCase.UpdatedAt = DateTime.UtcNow;
        supportCase.UpdatedBy = actorId;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(actorId, actorName, $"set support case status to {request.Status}", supportCase.Subject);

        // Let the institution that raised this ticket know it's been handled —
        // direct DB write (this service isn't tenant-scoped, so IgnoreQueryFilters
        // throughout) rather than going through Institution.Api's actor, which
        // has no route in from here.
        if (request.Status == "Resolved" && !wasAlreadyResolved && !string.IsNullOrEmpty(supportCase.InstitutionId))
        {
            var admins = await db.Set<StaffEntity>().IgnoreQueryFilters()
                .Where(s => s.InstitutionId == supportCase.InstitutionId && (s.Role == "Admin" || s.Role == "SuperAdmin") && !s.IsDisabled)
                .ToListAsync();

            foreach (var admin in admins)
            {
                db.Set<NotificationEntity>().Add(new NotificationEntity
                {
                    InstitutionId = supportCase.InstitutionId,
                    RecipientId = admin.Id,
                    RecipientType = "Admin",
                    Title = "Support ticket resolved",
                    Body = $"\"{supportCase.Subject}\" has been resolved by the platform team.",
                    Type = "SupportTicketResolved",
                    RelatedEntityId = supportCase.Id,
                    RelatedEntityType = "SupportCase",
                    ActionUrl = "/support",
                    CreatedBy = actorId,
                });
            }
            await db.SaveChangesAsync();
        }

        var dto = (await ToDtosAsync([supportCase])).Single();
        return dto.ToOkApiResponse("Status updated");
    }

    public async Task<IApiResponse<SupportCaseResponse>> AddNoteAsync(string id, AddInternalNoteRequest request, string actorId, string actorName)
    {
        var supportCase = await db.SupportCases.FirstOrDefaultAsync(c => c.Id == id);
        if (supportCase is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<SupportCaseResponse>("Support case not found");

        supportCase.InternalNote = request.Note;
        supportCase.AssigneeStaffId ??= actorId;
        supportCase.UpdatedAt = DateTime.UtcNow;
        supportCase.UpdatedBy = actorId;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(actorId, actorName, "added internal note", supportCase.Subject);

        var dto = (await ToDtosAsync([supportCase])).Single();
        return dto.ToOkApiResponse("Note added");
    }

    private async Task<List<SupportCaseResponse>> ToDtosAsync(List<SupportCase> cases)
    {
        var institutionIds = cases.Where(c => c.InstitutionId != null).Select(c => c.InstitutionId!).Distinct().ToList();
        var institutionNames = await db.Institutions.Where(i => institutionIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);

        var assigneeIds = cases.Where(c => c.AssigneeStaffId != null).Select(c => c.AssigneeStaffId!).Distinct().ToList();
        var assigneeNames = await db.PlatformStaff.Where(s => assigneeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        var now = DateTime.UtcNow;
        return cases.Select(c => new SupportCaseResponse(
            c.Id, c.Subject,
            c.InstitutionId, c.InstitutionId != null && institutionNames.TryGetValue(c.InstitutionId, out var iName) ? iName : null,
            c.Severity, c.Status,
            c.AssigneeStaffId, c.AssigneeStaffId != null && assigneeNames.TryGetValue(c.AssigneeStaffId, out var aName) ? aName : null,
            Math.Round((now - c.CreatedAt).TotalHours, 1),
            c.Requester, c.RequesterEmail, c.Message, c.InternalNote)).ToList();
    }
}
