using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Sms.Sdk.Services;
using ReservEase.Alumni.WebPush.Sdk.Services;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using PushSubscriptionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.PushSubscription;
using Temporalio.Activities;
using Temporalio.Exceptions;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Notifications;

/// <summary>
/// One activity, one I/O call — NotificationDispatchWorkflow owns the sequencing, the
/// preference/eligibility branching, and every message string. Registered via
/// AddScopedActivities, so each call gets its own DI scope automatically. Every read here
/// is tenant-agnostic (ignoreQueryFilters: true) since the workflow has no ambient tenant
/// context; every write that touches an ITenantScoped entity calls
/// ICurrentTenantService.SetInstitutionId first so AlumniDbContext's auto-stamp picks up
/// the right institution (mirrors ContributionCallbackActivities.DispatchContributionConfirmedAsync).
/// </summary>
public class NotificationDispatchActivities(
    IAlumniPgRepository<Notification> notifRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<AdminNotificationPreference> adminPrefRepo,
    IAlumniPgRepository<StaffEntity> adminRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<ClassNote> classNoteRepo,
    IAlumniPgRepository<PushSubscriptionEntity> pushSubscriptionRepo,
    ICurrentTenantService currentTenant,
    ISmsService smsService,
    IWhatsAppService whatsAppService,
    IEmailService emailService,
    IWebPushService webPushService,
    IConfiguration configuration,
    ILogger<NotificationDispatchActivities> logger)
{
    [Activity("NotificationDispatch.LoadInstitution")]
    public virtual async Task<InstitutionContactInfo?> LoadInstitutionAsync(string institutionId)
    {
        if (string.IsNullOrEmpty(institutionId)) return null;
        return await Wrap(async () =>
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            if (institution is null) return null;

            var memberDomain = configuration["MemberPortalBaseDomain"];
            var adminDomain = configuration["AdminPortalBaseDomain"];
            return new InstitutionContactInfo
            {
                Name = institution.Name,
                SmsNotificationsEnabled = institution.SmsNotificationsEnabled,
                MemberPortalUrl = string.IsNullOrWhiteSpace(memberDomain) ? string.Empty : $"https://{institution.Slug}.{memberDomain}",
                AdminPortalUrl = string.IsNullOrWhiteSpace(adminDomain) ? string.Empty : $"https://{institution.Slug}.{adminDomain}",
            };
        }, "load institution", institutionId);
    }

    [Activity("NotificationDispatch.LoadJobContent")]
    public virtual Task<JobAlertContent?> LoadJobContentAsync(string jobId) =>
        Wrap(async () =>
        {
            var job = await jobRepo.GetOneAsync(j => j.Id == jobId, ignoreQueryFilters: true);
            return job is null ? null : new JobAlertContent { Title = job.Title, Company = job.Company, Location = job.Location, YearGroups = job.YearGroups };
        }, "load job content", jobId);

    [Activity("NotificationDispatch.LoadCampaignContent")]
    public virtual Task<CampaignAlertContent?> LoadCampaignContentAsync(string campaignId) =>
        Wrap(async () =>
        {
            var campaign = await campaignRepo.GetOneAsync(c => c.Id == campaignId, ignoreQueryFilters: true);
            return campaign is null ? null : new CampaignAlertContent { Title = campaign.Title, YearGroups = campaign.YearGroups };
        }, "load campaign content", campaignId);

    [Activity("NotificationDispatch.LoadEventContent")]
    public virtual Task<EventReminderContent?> LoadEventContentAsync(string eventId) =>
        Wrap(async () =>
        {
            var ev = await eventRepo.GetOneAsync(e => e.Id == eventId, ignoreQueryFilters: true);
            return ev is null ? null : new EventReminderContent { Title = ev.Title, StartDate = ev.StartDate, Venue = ev.Venue, YearGroups = ev.YearGroups };
        }, "load event content", eventId);

    [Activity("NotificationDispatch.LoadSpotlightContent")]
    public virtual Task<SpotlightAlertContent?> LoadSpotlightContentAsync(string spotlightId) =>
        Wrap(async () =>
        {
            var spotlight = await spotlightRepo.GetOneAsync(s => s.Id == spotlightId, ignoreQueryFilters: true);
            return spotlight is null ? null : new SpotlightAlertContent
            {
                Title = spotlight.Title,
                MemberFirstName = spotlight.Member?.FirstName,
                MemberLastName = spotlight.Member?.LastName,
            };
        }, "load spotlight content", spotlightId);

    [Activity("NotificationDispatch.LoadClassNoteContent")]
    public virtual Task<ClassNoteAlertContent?> LoadClassNoteContentAsync(string noteId) =>
        Wrap(async () =>
        {
            var note = await classNoteRepo.GetOneAsync(n => n.Id == noteId, ignoreQueryFilters: true);
            return note is null ? null : new ClassNoteAlertContent { AuthorId = note.AuthorId, YearGroup = note.YearGroup };
        }, "load class note content", noteId);

    [Activity("NotificationDispatch.ResolveJobAlertRecipients")]
    public virtual Task<List<NotificationRecipient>> ResolveJobAlertRecipientsAsync(string institutionId, List<int>? yearGroups) =>
        ResolveMemberFanOutAsync(institutionId, p => !p.JobAlerts, m => yearGroups == null || yearGroups.Count == 0 || yearGroups.Contains(m.GraduationYear));

    [Activity("NotificationDispatch.ResolveCampaignAlertRecipients")]
    public virtual Task<List<NotificationRecipient>> ResolveCampaignAlertRecipientsAsync(string institutionId, List<int>? yearGroups) =>
        ResolveMemberFanOutAsync(institutionId, p => !p.CampaignAlerts, m => yearGroups == null || yearGroups.Count == 0 || yearGroups.Contains(m.GraduationYear));

    [Activity("NotificationDispatch.ResolveEventReminderRecipients")]
    public virtual Task<List<NotificationRecipient>> ResolveEventReminderRecipientsAsync(string institutionId, List<int>? yearGroups) =>
        ResolveMemberFanOutAsync(institutionId, p => !p.EventReminders, m => yearGroups == null || yearGroups.Count == 0 || yearGroups.Contains(m.GraduationYear));

    [Activity("NotificationDispatch.ResolveSpotlightAlertRecipients")]
    public virtual Task<List<NotificationRecipient>> ResolveSpotlightAlertRecipientsAsync(string institutionId) =>
        ResolveMemberFanOutAsync(institutionId, p => !p.SpotlightAlerts, _ => true);

    [Activity("NotificationDispatch.ResolveClassNoteAlertRecipients")]
    public virtual Task<List<NotificationRecipient>> ResolveClassNoteAlertRecipientsAsync(string institutionId, int yearGroup, string authorId) =>
        ResolveMemberFanOutAsync(institutionId, p => !p.ClassNoteAlerts || p.MemberId == authorId, m => m.GraduationYear == yearGroup && m.Id != authorId);

    /// <summary>Shared shape behind every member fan-out above: treat an absent preference
    /// row as "everything on" (only explicit opt-outs, matched by optedOutPredicate, are
    /// excluded), then filter the remaining active members by extraFilter, then bundle
    /// each with their SMS/WhatsApp eligibility in one more read. Not itself an [Activity]
    /// — the six methods above each need their own activity name (a lambda predicate can't
    /// cross the Temporal activity boundary), but they all share this exact query shape.</summary>
    private async Task<List<NotificationRecipient>> ResolveMemberFanOutAsync(
        string institutionId, Func<NotificationPreference, bool> optedOutPredicate, Func<MemberEntity, bool> extraFilter)
    {
        return await Wrap(async () =>
        {
            var allPrefs = await prefRepo.GetAllAsync(p => p.InstitutionId == institutionId, ignoreQueryFilters: true);
            var optedOutIds = allPrefs.Where(optedOutPredicate).Select(p => p.MemberId).ToHashSet();

            var members = (await memberRepo.GetAllAsync(
                    m => m.InstitutionId == institutionId && !optedOutIds.Contains(m.Id) && m.Status == "Active",
                    ignoreQueryFilters: true))
                .Where(extraFilter)
                .ToList();

            var memberIds = members.Select(m => m.Id).ToHashSet();
            var prefsById = allPrefs.Where(p => memberIds.Contains(p.MemberId)).ToDictionary(p => p.MemberId);

            return members.Select(m =>
            {
                var pref = prefsById.GetValueOrDefault(m.Id);
                return new NotificationRecipient
                {
                    MemberId = m.Id,
                    Phone = m.Phone,
                    SmsAlerts = pref?.SmsAlerts ?? false,
                    WhatsAppAlerts = pref?.WhatsAppAlerts ?? false,
                };
            }).ToList();
        }, "resolve member fan-out", institutionId);
    }

    [Activity("NotificationDispatch.ResolvePaymentReceivedAdminRecipients")]
    public virtual Task<List<string>> ResolvePaymentReceivedAdminRecipientsAsync(string institutionId) =>
        Wrap(async () =>
        {
            var optedOut = await adminPrefRepo.GetAllAsync(p => p.InstitutionId == institutionId && !p.PaymentReceivedAlerts, ignoreQueryFilters: true);
            var optedOutIds = optedOut.Select(p => p.StaffId).ToHashSet();
            var admins = await adminRepo.GetAllAsync(a => a.InstitutionId == institutionId && !a.IsDisabled && !optedOutIds.Contains(a.Id), ignoreQueryFilters: true);
            return admins.Select(a => a.Id).ToList();
        }, "resolve payment received admin recipients", institutionId);

    [Activity("NotificationDispatch.ResolvePendingApprovalAdminRecipients")]
    public virtual Task<List<string>> ResolvePendingApprovalAdminRecipientsAsync(string institutionId) =>
        Wrap(async () =>
        {
            var optedOut = await adminPrefRepo.GetAllAsync(p => p.InstitutionId == institutionId && !p.PendingApprovalAlerts, ignoreQueryFilters: true);
            var optedOutIds = optedOut.Select(p => p.StaffId).ToHashSet();
            var admins = await adminRepo.GetAllAsync(a => a.InstitutionId == institutionId && !a.IsDisabled && !optedOutIds.Contains(a.Id), ignoreQueryFilters: true);
            return admins.Select(a => a.Id).ToList();
        }, "resolve pending approval admin recipients", institutionId);

    [Activity("NotificationDispatch.LoadMemberWithPreference")]
    public virtual Task<MemberWithPreference?> LoadMemberWithPreferenceAsync(string memberId) =>
        Wrap(async () =>
        {
            var member = await memberRepo.GetOneAsync(m => m.Id == memberId, ignoreQueryFilters: true);
            if (member is null) return null;

            var pref = await prefRepo.GetOneAsync(p => p.MemberId == memberId, ignoreQueryFilters: true);
            return new MemberWithPreference
            {
                MemberId = member.Id,
                Email = member.Email,
                FirstName = member.FirstName,
                Phone = member.Phone,
                SmsAlerts = pref?.SmsAlerts ?? false,
                WhatsAppAlerts = pref?.WhatsAppAlerts ?? false,
                EventReminders = pref?.EventReminders ?? true,
            };
        }, "load member with preference", memberId);

    [Activity("NotificationDispatch.CreateNotification")]
    public virtual Task CreateNotificationAsync(string institutionId, Notification notification) =>
        Wrap(async () =>
        {
            currentTenant.SetInstitutionId(institutionId);
            var duplicate = await notifRepo.GetOneAsync(n =>
                n.InstitutionId == institutionId &&
                n.RecipientId == notification.RecipientId &&
                n.Type == notification.Type &&
                n.RelatedEntityId == notification.RelatedEntityId &&
                n.Body == notification.Body, ignoreQueryFilters: true);
            if (duplicate is null)
                await notifRepo.AddAsync(notification);
        }, "create notification", notification.RecipientId);

    [Activity("NotificationDispatch.CreateNotifications")]
    public virtual Task CreateNotificationsAsync(string institutionId, List<Notification> notifications) =>
        Wrap(async () =>
        {
            currentTenant.SetInstitutionId(institutionId);
            var newNotifications = new List<Notification>();
            foreach (var notification in notifications)
            {
                var duplicate = await notifRepo.GetOneAsync(n =>
                    n.InstitutionId == institutionId &&
                    n.RecipientId == notification.RecipientId &&
                    n.Type == notification.Type &&
                    n.RelatedEntityId == notification.RelatedEntityId &&
                    n.Body == notification.Body, ignoreQueryFilters: true);
                if (duplicate is null)
                    newNotifications.Add(notification);
            }

            if (newNotifications.Count > 0)
                await notifRepo.AddRangeAsync(newNotifications);
        }, "create notifications", institutionId);

    /// <summary>
    /// Returns the gateway's own success flag rather than discarding it — SmsService/
    /// WhatsAppService/EmailService all swallow their own exceptions and report failure via
    /// a bool/Success field instead of throwing, so a caller that ignores this return value
    /// (as these activities previously did by declaring a bare Task) never learns a send
    /// failed: Temporal sees the activity complete "successfully" regardless, and the
    /// ExternalGateway RetryPolicy never gets a chance to trigger since nothing ever throws.
    /// </summary>
    [Activity("NotificationDispatch.SendSms")]
    public virtual Task<bool> SendSmsAsync(string phone, string message) =>
        Wrap(() => smsService.SendSmsAsync(phone, message), "send SMS", phone);

    [Activity("NotificationDispatch.SendWhatsApp")]
    public virtual Task<bool> SendWhatsAppAsync(string phone, string message) =>
        Wrap(() => whatsAppService.SendMessageAsync(phone, message), "send WhatsApp message", phone);

    [Activity("NotificationDispatch.SendEmail")]
    public virtual Task<bool> SendEmailAsync(SendEmailRequest request, string context) =>
        Wrap(async () => (await emailService.SendEmailAsync(request)).Success, "send email", context);

    /// <summary>Loops over every active subscription for this owner (a member/staff account
    /// can have several — one per browser/device) rather than the workflow scheduling one
    /// activity call per device, keeping fan-out and 410/404 cleanup co-located here. Safe
    /// to retry as a whole: re-sending to an already-sent subscription is harmless, and
    /// re-marking a gone subscription inactive is idempotent.</summary>
    [Activity("NotificationDispatch.SendWebPush")]
    public virtual Task<bool> SendWebPushAsync(string ownerId, string ownerType, string title, string body, string? actionUrl) =>
        Wrap(async () =>
        {
            var subscriptions = (await pushSubscriptionRepo.GetAllAsync(
                s => s.OwnerId == ownerId && s.OwnerType == ownerType && s.IsActive, ignoreQueryFilters: true)).ToList();
            if (subscriptions.Count == 0) return false;

            var anySent = false;
            foreach (var sub in subscriptions)
            {
                var dto = new PushSubscriptionDto(sub.Endpoint, sub.P256dhKey, sub.AuthKey);
                var result = await webPushService.SendAsync(dto, title, body, actionUrl);
                switch (result)
                {
                    case WebPushSendResult.Sent:
                        sub.LastUsedAt = DateTime.UtcNow;
                        anySent = true;
                        break;
                    case WebPushSendResult.Gone:
                        sub.IsActive = false;
                        sub.LastFailedAt = DateTime.UtcNow;
                        break;
                    default:
                        sub.LastFailedAt = DateTime.UtcNow;
                        break;
                }
                await pushSubscriptionRepo.UpdateAsync(sub);
            }
            return anySent;
        }, "send web push", $"{ownerType}:{ownerId}");

    private static async Task<T> Wrap<T>(Func<Task<T>> action, string what, string context)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }

    private static async Task Wrap(Func<Task> action, string what, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }
}
