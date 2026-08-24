using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class AnnouncementService(AlumniDbContext db, IAuditLogService auditLog) : IAnnouncementService
{
    public async Task<IApiResponse<List<AnnouncementResponse>>> GetAnnouncementsAsync()
    {
        var items = await db.Announcements
            .OrderByDescending(a => a.SentAt)
            .Select(a => new AnnouncementResponse(a.Id, a.Title, a.Body, a.Audience, a.SentAt, a.SeenByAdmins, a.TotalAdmins))
            .ToListAsync();
        return items.ToOkApiResponse();
    }

    public async Task<IApiResponse<AnnouncementResponse>> SendAsync(SendAnnouncementRequest request, string actorId, string actorName)
    {
        var totalAdmins = await db.Set<StaffEntity>().IgnoreQueryFilters().CountAsync(s => !s.IsDisabled);

        var announcement = new Announcement
        {
            Title = request.Title,
            Body = request.Body,
            Audience = request.Audience,
            SentAt = DateTime.UtcNow,
            TotalAdmins = totalAdmins,
            SeenByAdmins = 0,
            CreatedBy = actorId,
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();

        await auditLog.LogAsync(actorId, actorName, "sent announcement", announcement.Title);

        return new AnnouncementResponse(announcement.Id, announcement.Title, announcement.Body, announcement.Audience, announcement.SentAt, announcement.SeenByAdmins, announcement.TotalAdmins)
            .ToCreatedApiResponse();
    }
}
