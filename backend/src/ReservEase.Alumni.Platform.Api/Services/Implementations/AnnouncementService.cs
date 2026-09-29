using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Sms.Sdk.Services;
using NotificationEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Notification;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class AnnouncementService(
    AlumniDbContext db,
    IAlumniPgRepository<Announcement> announcementRepo,
    IAlumniPgRepository<StaffEntity> staffRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<NotificationEntity> notificationRepo,
    IEmailService emailService,
    ISmsService smsService,
    IAuditLogService auditLog,
    IConfiguration configuration,
    ILogger<AnnouncementService> logger) : IAnnouncementService
{
    public async Task<IApiResponse<List<AnnouncementResponse>>> GetAnnouncementsAsync()
    {
        try
        {
            var items = await announcementRepo.GetQueryable()
                .OrderByDescending(a => a.SentAt)
                .Select(a => new AnnouncementResponse(a.Id, a.Title, a.Body, a.Audience, a.SentAt, a.SeenByAdmins, a.TotalAdmins,
                    a.Channels, a.EmailSent, a.SmsSent, a.SmsSkippedNoPhone))
                .ToListAsync();
            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetAnnouncementsAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<AnnouncementResponse>>("Failed to get announcements");
        }
    }

    public async Task<IApiResponse<List<StaffDirectoryEntry>>> SearchStaffAsync(string? search, string? institutionId)
    {
        try
        {
            var term = search?.Trim().ToLower();
            var staff = await staffRepo.GetQueryable(s => !s.IsDisabled
                    && (string.IsNullOrEmpty(institutionId) || s.InstitutionId == institutionId)
                    && (string.IsNullOrEmpty(term)
                        || s.FirstName.ToLower().Contains(term) || s.LastName.ToLower().Contains(term) || s.Email.ToLower().Contains(term)),
                    ignoreQueryFilters: true)
                .OrderBy(s => s.FirstName).Take(50)
                .ToListAsync();

            var institutionIds = staff.Select(s => s.InstitutionId).Distinct().ToList();
            var institutions = await institutionRepo.GetAllAsync(i => institutionIds.Contains(i.Id), ignoreQueryFilters: true);
            var names = institutions.ToDictionary(i => i.Id, i => i.Name);

            var result = staff.Select(s => new StaffDirectoryEntry(
                s.Id, s.FirstName, s.LastName, s.Email, s.Role, !string.IsNullOrWhiteSpace(s.Phone),
                s.InstitutionId, names.GetValueOrDefault(s.InstitutionId, "Unknown institution"))).ToList();
            return result.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "SearchStaffAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<StaffDirectoryEntry>>("Failed to search staff");
        }
    }

    public async Task<IApiResponse<AnnouncementResponse>> SendAsync(SendAnnouncementRequest request, string actorId, string actorName)
    {
        try
        {
            var channels = request.Channels.Count == 0 ? [NotificationChannels.InApp] : request.Channels.Distinct().ToList();

            // Who gets it: an explicit pick of staff (any institution) beats "every admin of one institution",
            // which beats the original, broadest behaviour of every admin, everywhere.
            List<StaffEntity> recipients;
            string audience;
            if (request.RecipientStaffIds.Count > 0)
            {
                recipients = (await staffRepo.GetAllAsync(s => request.RecipientStaffIds.Contains(s.Id), ignoreQueryFilters: true)).ToList();
                audience = recipients.Count == 1
                    ? $"{recipients[0].FirstName} {recipients[0].LastName}"
                    : $"{recipients.Count} selected admins";
            }
            else if (!string.IsNullOrWhiteSpace(request.InstitutionId))
            {
                recipients = (await staffRepo.GetAllAsync(s => !s.IsDisabled && s.InstitutionId == request.InstitutionId, ignoreQueryFilters: true)).ToList();
                var institution = await institutionRepo.GetByIdAsync(request.InstitutionId, ignoreQueryFilters: true);
                audience = $"All admins — {institution?.Name ?? "one institution"}";
            }
            else
            {
                recipients = (await staffRepo.GetAllAsync(s => !s.IsDisabled, ignoreQueryFilters: true)).ToList();
                audience = "All institutions";
            }

            var announcement = new Announcement
            {
                Title = request.Title,
                Body = request.Body,
                Audience = audience,
                Channels = channels,
                SentAt = DateTime.UtcNow,
                TotalAdmins = recipients.Count,
                SeenByAdmins = 0,
                CreatedBy = actorId,
            };

            // Built now but persisted last, inside the same transaction as the Announcement row
            // (see below) — this is what keeps a later failure (e.g. the jsonb-serialization bug
            // that hit this exact spot before) from leaving in-app notifications behind for a
            // retried send to duplicate.
            var notifications = channels.Contains(NotificationChannels.InApp)
                ? recipients.Select(recipient => new NotificationEntity
                {
                    InstitutionId = recipient.InstitutionId,
                    RecipientId = recipient.Id,
                    RecipientType = "Admin",
                    Title = request.Title,
                    Body = request.Body,
                    Type = "PlatformAnnouncement",
                    CreatedBy = actorId,
                }).ToList()
                : [];

            if (channels.Contains(NotificationChannels.Email))
            {
                // Each admin's own institution subdomain, batched once rather than per recipient —
                // recipients can span many institutions when the audience is "All institutions".
                var adminDomain = configuration["AdminPortalBaseDomain"];
                var slugs = string.IsNullOrWhiteSpace(adminDomain)
                    ? new Dictionary<string, string>()
                    : (await institutionRepo.GetAllAsync(
                            i => recipients.Select(r => r.InstitutionId).Distinct().Contains(i.Id), ignoreQueryFilters: true))
                        .ToDictionary(i => i.Id, i => i.Slug);

                var sent = 0;
                foreach (var r in recipients)
                {
                    try
                    {
                        var actionUrl = !string.IsNullOrWhiteSpace(adminDomain) && slugs.TryGetValue(r.InstitutionId, out var slug)
                            ? $"https://{slug}.{adminDomain}"
                            : string.Empty;

                        var response = await emailService.SendEmailAsync(new SendEmailRequest
                        {
                            To = [new EmailContact { Email = r.Email, Name = $"{r.FirstName} {r.LastName}".Trim() }],
                            TemplateId = "notification",
                            TemplateVariables = new
                            {
                                first_name = r.FirstName,
                                title = request.Title,
                                body = request.Body,
                                badge_label = "Notice from AlumUnion",
                                pref_label = "you are listed as an administrator on AlumUnion",
                                action_url = actionUrl,
                                action_label = "Open your portal",
                            },
                        });
                        if (response.Success) sent++;
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to email announcement to admin {AdminId}", r.Id);
                    }
                }
                announcement.EmailSent = sent;
            }

            if (channels.Contains(NotificationChannels.Sms))
            {
                var withPhone = recipients.Where(r => !string.IsNullOrWhiteSpace(r.Phone)).ToList();
                announcement.SmsSkippedNoPhone = recipients.Count - withPhone.Count;
                var sent = 0;
                var smsText = ToSmsSafe($"AlumUnion: {request.Title}\n{request.Body}");
                foreach (var r in withPhone)
                {
                    if (await smsService.SendSmsAsync(r.Phone!, smsText)) sent++;
                }
                announcement.SmsSent = sent;
            }

            // Everything below is a DB write; one transaction so a failure partway through
            // (e.g. the Announcement row) rolls the in-app notifications back too, instead of
            // leaving them orphaned for a client retry to duplicate.
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (notifications.Count > 0)
                await notificationRepo.AddRangeAsync(notifications);
            await announcementRepo.AddAsync(announcement);
            await auditLog.LogAsync(actorId, actorName, "sent notification", $"{announcement.Title} ({audience}, via {string.Join("+", channels)})");
            await transaction.CommitAsync();

            return new AnnouncementResponse(announcement.Id, announcement.Title, announcement.Body, announcement.Audience,
                    announcement.SentAt, announcement.SeenByAdmins, announcement.TotalAdmins,
                    announcement.Channels, announcement.EmailSent, announcement.SmsSent, announcement.SmsSkippedNoPhone)
                .ToCreatedApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "SendAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<AnnouncementResponse>("Failed to send");
        }
    }

    /// <summary>Same alphabet fix the notification worker uses — an em dash or curly quote pushes a text onto the
    /// costlier multi-part SMS encoding.</summary>
    private static string ToSmsSafe(string message) => message
        .Replace("—", "-").Replace("–", "-")
        .Replace("‘", "'").Replace("’", "'")
        .Replace("“", "\"").Replace("”", "\"")
        .Replace("…", "...").Replace("₵", "GHS ");
}
