using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using NotificationEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Notification;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class AnnouncementService(
    IAlumniPgRepository<Announcement> announcementRepo,
    IAlumniPgRepository<StaffEntity> staffRepo,
    IAlumniPgRepository<NotificationEntity> notificationRepo,
    IAuditLogService auditLog) : IAnnouncementService
{
    public async Task<IApiResponse<List<AnnouncementResponse>>> GetAnnouncementsAsync()
    {
        var items = await announcementRepo.GetQueryable()
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
        var recipients = await staffRepo.GetQueryable(s => !s.IsDisabled, ignoreQueryFilters: true)
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
        await announcementRepo.AddAsync(announcement);

        var notifications = recipients.Select(recipient => new NotificationEntity
        {
            InstitutionId = recipient.InstitutionId,
            RecipientId = recipient.Id,
            RecipientType = "Admin",
            Title = request.Title,
            Body = request.Body,
            Type = "PlatformAnnouncement",
            CreatedBy = actorId,
        }).ToList();

        if (notifications.Count > 0)
            await notificationRepo.AddRangeAsync(notifications);

        await auditLog.LogAsync(actorId, actorName, "sent announcement", announcement.Title);

        return new AnnouncementResponse(announcement.Id, announcement.Title, announcement.Body, announcement.Audience, announcement.SentAt, announcement.SeenByAdmins, announcement.TotalAdmins)
            .ToCreatedApiResponse();
    }
}
