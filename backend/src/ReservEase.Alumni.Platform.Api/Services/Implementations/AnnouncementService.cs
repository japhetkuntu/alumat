using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using NotificationEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Notification;
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
        // "All institutions" is the only audience the platform UI currently
        // offers (see the announcements page) — every non-disabled admin,
        // across every institution, gets an in-app notification fanned out
        // directly (Platform.Api has no actor system of its own and doesn't
        // need one for a single batch write like this).
        var recipients = await db.Set<StaffEntity>().IgnoreQueryFilters()
            .Where(s => !s.IsDisabled)
            .Select(s => new { s.Id, s.InstitutionId })
            .ToListAsync();

        var announcement = new Announcement
        {
            Title = request.Title,
            Body = request.Body,
            Audience = request.Audience,
            SentAt = DateTime.UtcNow,
            TotalAdmins = recipients.Count,
            SeenByAdmins = 0,
            CreatedBy = actorId,
        };
        db.Announcements.Add(announcement);

        foreach (var recipient in recipients)
        {
            db.Set<NotificationEntity>().Add(new NotificationEntity
            {
                InstitutionId = recipient.InstitutionId,
                RecipientId = recipient.Id,
                RecipientType = "Admin",
                Title = request.Title,
                Body = request.Body,
                Type = "PlatformAnnouncement",
                CreatedBy = actorId,
            });
        }

        await db.SaveChangesAsync();

        await auditLog.LogAsync(actorId, actorName, "sent announcement", announcement.Title);

        return new AnnouncementResponse(announcement.Id, announcement.Title, announcement.Body, announcement.Audience, announcement.SentAt, announcement.SeenByAdmins, announcement.TotalAdmins)
            .ToCreatedApiResponse();
    }
}
