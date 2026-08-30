using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Sms.Sdk.Services;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class NotificationDispatcher(
    IAlumniPgRepository<Notification> notifRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<AdminNotificationPreference> adminPrefRepo,
    IAlumniPgRepository<StaffEntity> adminRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant,
    ISmsService smsService,
    IConfiguration configuration,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    /// <summary>
    /// The current institution, fetched once per dispatch and reused for both
    /// the SMS name-prefix and the portal-URL builders below — avoids querying
    /// it once per recipient in a batch fan-out.
    /// </summary>
    private InstitutionEntity? cachedInstitution;
    private bool institutionLoaded;
    private async Task<InstitutionEntity?> GetInstitutionAsync()
    {
        if (institutionLoaded) return cachedInstitution;
        institutionLoaded = true;
        if (!string.IsNullOrEmpty(currentTenant.InstitutionId))
            cachedInstitution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        return cachedInstitution;
    }

    private async Task<string?> GetInstitutionNameAsync() => (await GetInstitutionAsync())?.Name;

    /// <summary>
    /// Builds this institution's live subdomain URL from its slug + the
    /// configured base domain (same "{slug}.{domain}" convention used by
    /// InstitutionController.ToDto and Platform.Api's InstitutionManagementService)
    /// instead of a hardcoded localhost literal. Falls back to an empty string
    /// (relative-looking action URL) if the domain isn't configured, rather than
    /// ever pointing a notification at a dev-only address in production.
    /// </summary>
    private async Task<string> GetMemberPortalUrlAsync()
    {
        var institution = await GetInstitutionAsync();
        var domain = configuration["MemberPortalBaseDomain"];
        return institution is null || string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{institution.Slug}.{domain}";
    }

    /// <summary>
    /// This API's own PlatformBaseDomain config value already *is* the admin
    /// portal's base domain (see InstitutionController's comment on the same
    /// convention) — distinct from Platform.Api's own "PlatformBaseDomain",
    /// which means something else there.
    /// </summary>
    private async Task<string> GetAdminPortalUrlAsync()
    {
        var institution = await GetInstitutionAsync();
        var domain = configuration["PlatformBaseDomain"];
        return institution is null || string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{institution.Slug}.{domain}";
    }

    /// <summary>
    /// Fires SMS for a member if they've opted in and have a phone number on
    /// file. SendSmsAsync already swallows its own failures (log + return
    /// false) so a gateway outage never breaks the caller's in-app flow.
    /// </summary>
    private async Task SendExternalAlertsAsync(MemberEntity member, NotificationPreference? pref, string message)
    {
        if (string.IsNullOrWhiteSpace(member.Phone) || pref is null || !pref.SmsAlerts) return;

        var institutionName = await GetInstitutionNameAsync();
        var smsMessage = string.IsNullOrWhiteSpace(institutionName) ? message : $"{institutionName}: {message}";
        await smsService.SendSmsAsync(member.Phone, smsMessage);
    }

    // ── Member fan-outs ─────────────────────────────────────────────────────

    public async Task DispatchJobAlertAsync(Job job)
    {
        try
        {
            // Treat absent preference rows as defaults (all alerts = true).
            // Only exclude members who have explicitly opted out.
            var optedOut = await prefRepo.GetAllAsync(p => !p.JobAlerts);
            var optedOutIds = optedOut.Select(p => p.MemberId).ToHashSet();
            var members = await memberRepo.GetAllAsync(m =>
                !optedOutIds.Contains(m.Id)
                && m.Status == "Active"
                && (job.YearGroups == null || job.YearGroups.Count == 0 || job.YearGroups.Contains(m.GraduationYear)));

            var portalUrl = await GetMemberPortalUrlAsync();
            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Job Posting",
                Body = $"{job.Title} at {job.Company} — {job.Location}",
                Type = "JobAlert",
                RelatedEntityId = job.Id,
                RelatedEntityType = "Job",
                ActionUrl = $"{portalUrl}/jobs/{job.Id}",
                CreatedBy = "system",
            }).ToList();

            if (notifications.Count > 0)
                await notifRepo.AddRangeAsync(notifications);

            logger.LogInformation("Dispatched JobAlert for job {JobId} to {Count} members", job.Id, notifications.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch JobAlert for job {JobId}", job.Id);
        }
    }

    public async Task DispatchCampaignAlertAsync(Campaign campaign)
    {
        try
        {
            // Treat absent preference rows as defaults (all alerts = true).
            // Only exclude members who have explicitly opted out.
            var optedOut = await prefRepo.GetAllAsync(p => !p.CampaignAlerts);
            var optedOutIds = optedOut.Select(p => p.MemberId).ToHashSet();
            var members = await memberRepo.GetAllAsync(m =>
                !optedOutIds.Contains(m.Id)
                && m.Status == "Active"
                && (campaign.YearGroups == null || campaign.YearGroups.Count == 0 || campaign.YearGroups.Contains(m.GraduationYear)));

            var portalUrl = await GetMemberPortalUrlAsync();
            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Campaign Launched",
                Body = campaign.Title,
                Type = "CampaignAlert",
                RelatedEntityId = campaign.Id,
                RelatedEntityType = "Campaign",
                ActionUrl = $"{portalUrl}/contributions",
                CreatedBy = "system",
            }).ToList();

            if (notifications.Count > 0)
                await notifRepo.AddRangeAsync(notifications);

            logger.LogInformation("Dispatched CampaignAlert for campaign {CampaignId} to {Count} members", campaign.Id, notifications.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch CampaignAlert for campaign {CampaignId}", campaign.Id);
        }
    }

    public async Task DispatchEventReminderAsync(AlumniEvent ev)
    {
        try
        {
            // Treat absent preference rows as defaults (all alerts = true).
            // Only exclude members who have explicitly opted out.
            var optedOut = await prefRepo.GetAllAsync(p => !p.EventReminders);
            var optedOutIds = optedOut.Select(p => p.MemberId).ToHashSet();
            var members = await memberRepo.GetAllAsync(m =>
                !optedOutIds.Contains(m.Id)
                && m.Status == "Active"
                && (ev.YearGroups == null || ev.YearGroups.Count == 0 || ev.YearGroups.Contains(m.GraduationYear)));

            var dateStr = ev.StartDate.ToString("dddd, MMMM d yyyy");
            var portalUrl = await GetMemberPortalUrlAsync();
            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "Upcoming Event",
                Body = $"{ev.Title} — {dateStr} at {ev.Venue}",
                Type = "EventReminder",
                RelatedEntityId = ev.Id,
                RelatedEntityType = "Event",
                ActionUrl = $"{portalUrl}/events/{ev.Id}",
                CreatedBy = "system",
            }).ToList();

            if (notifications.Count > 0)
                await notifRepo.AddRangeAsync(notifications);

            logger.LogInformation("Dispatched EventReminder for event {EventId} to {Count} members", ev.Id, notifications.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch EventReminder for event {EventId}", ev.Id);
        }
    }

    public async Task DispatchSpotlightAlertAsync(Spotlight spotlight)
    {
        try
        {
            // Treat absent preference rows as defaults (all alerts = true).
            // Only exclude members who have explicitly opted out.
            var optedOut = await prefRepo.GetAllAsync(p => !p.SpotlightAlerts);
            var optedOutIds = optedOut.Select(p => p.MemberId).ToHashSet();
            var members = await memberRepo.GetAllAsync(m => !optedOutIds.Contains(m.Id) && m.Status == "Active");

            var memberName = spotlight.Member != null
                ? $"{spotlight.Member.FirstName} {spotlight.Member.LastName}"
                : "An alumnus";

            var portalUrl = await GetMemberPortalUrlAsync();
            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Alumni Spotlight",
                Body = $"{memberName} — {spotlight.Title}",
                Type = "SpotlightUpdate",
                RelatedEntityId = spotlight.Id,
                RelatedEntityType = "Spotlight",
                ActionUrl = $"{portalUrl}/spotlights",
                CreatedBy = "system",
            }).ToList();

            if (notifications.Count > 0)
                await notifRepo.AddRangeAsync(notifications);

            logger.LogInformation("Dispatched SpotlightAlert for spotlight {SpotlightId} to {Count} members", spotlight.Id, notifications.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch SpotlightAlert for spotlight {SpotlightId}", spotlight.Id);
        }
    }

    // ── Admin notifications ─────────────────────────────────────────────────

    public async Task DispatchPaymentReceivedToAdminsAsync(
        string memberName, string memberEmail, decimal amount, string campaignTitle, string contributionId)
    {
        try
        {
            // Treat absent preference rows as defaults (all alerts = true).
            // Only exclude admins who have explicitly opted out.
            var optedOut = await adminPrefRepo.GetAllAsync(p => !p.PaymentReceivedAlerts);
            var optedOutIds = optedOut.Select(p => p.StaffId).ToHashSet();
            var admins = (await adminRepo.GetAllAsync(a => !a.IsDisabled && !optedOutIds.Contains(a.Id))).ToList();
            var body = $"{memberName} ({memberEmail}) submitted a payment of GHS {amount:N2} for the campaign \"{campaignTitle}\". Please review and confirm.";
            var portalUrl = await GetAdminPortalUrlAsync();

            var notifications = admins.Select(a => new Notification
            {
                RecipientId = a.Id,
                RecipientType = "Admin",
                Title = "Payment Submitted",
                Body = body,
                Type = "PaymentReceived",
                RelatedEntityId = contributionId,
                RelatedEntityType = "Contribution",
                ActionUrl = $"{portalUrl}/contributions",
                CreatedBy = "system",
            }).ToList();

            if (notifications.Count > 0)
                await notifRepo.AddRangeAsync(notifications);

            logger.LogInformation("Dispatched PaymentReceived admin notification for contribution {ContributionId} to {Count} admins", contributionId, admins.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch PaymentReceived admin notification for contribution {ContributionId}", contributionId);
        }
    }

    public async Task DispatchContributionConfirmedAsync(
        string memberId, string memberEmail, string memberFirstName, decimal amount, string campaignTitle, string contributionId)
    {
        try
        {
            var notification = new Notification
            {
                RecipientId = memberId,
                RecipientType = "Member",
                Title = "Contribution Confirmed",
                Body = string.IsNullOrWhiteSpace(memberFirstName)
                    ? $"Your payment of GHS {amount:N2} for \"{campaignTitle}\" has been received. Thank you!"
                    : $"Thank you, {memberFirstName} — your payment of GHS {amount:N2} for \"{campaignTitle}\" has been received.",
                Type = "ContributionConfirmed",
                RelatedEntityId = contributionId,
                RelatedEntityType = "Contribution",
                ActionUrl = $"{await GetMemberPortalUrlAsync()}/contributions",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is not null)
            {
                var pref = await prefRepo.GetOneAsync(p => p.MemberId == memberId);
                await SendExternalAlertsAsync(member, pref, notification.Body);
            }

            logger.LogInformation("Dispatched ContributionConfirmed to member {MemberId}", memberId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch ContributionConfirmed for contribution {ContributionId}", contributionId);
        }
    }

    public async Task DispatchContributionRejectedAsync(
        string memberId, string memberEmail, string memberFirstName, string campaignTitle, string? reason, string contributionId)
    {
        try
        {
            var body = string.IsNullOrWhiteSpace(reason)
                ? $"Your contribution for \"{campaignTitle}\" was not confirmed. Please contact support or resubmit."
                : $"Your contribution for \"{campaignTitle}\" was not confirmed. Reason: {reason}. Please resubmit or contact support.";

            var notification = new Notification
            {
                RecipientId = memberId,
                RecipientType = "Member",
                Title = "Contribution Not Confirmed",
                Body = body,
                Type = "ContributionRejected",
                RelatedEntityId = contributionId,
                RelatedEntityType = "Contribution",
                ActionUrl = $"{await GetMemberPortalUrlAsync()}/contributions",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

            logger.LogInformation("Dispatched ContributionRejected to member {MemberId}", memberId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch ContributionRejected for contribution {ContributionId}", contributionId);
        }
    }

    public async Task DispatchMentorProfileDecisionAsync(string memberId, bool approved, string profileId)
    {
        try
        {
            var notification = new Notification
            {
                RecipientId = memberId,
                RecipientType = "Member",
                Title = approved ? "Mentor Application Approved" : "Mentor Application Not Approved",
                Body = approved
                    ? "Congratulations — your mentor application has been approved. You can now receive mentorship requests."
                    : "Your mentor application was not approved this time.",
                Type = "MentorProfileDecision",
                RelatedEntityId = profileId,
                RelatedEntityType = "MentorProfile",
                ActionUrl = $"{await GetMemberPortalUrlAsync()}/mentorship",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

            logger.LogInformation("Dispatched MentorProfileDecision ({Approved}) to member {MemberId}", approved, memberId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch MentorProfileDecision for mentor profile {ProfileId}", profileId);
        }
    }

    public async Task DispatchStoreDeliveryStatusUpdatedAsync(string memberId, string orderId, string orderNumber, string newStatus)
    {
        try
        {
            var notification = new Notification
            {
                RecipientId = memberId,
                RecipientType = "Member",
                Title = "Order Delivery Update",
                Body = $"Your order #{orderNumber} is now \"{newStatus}\".",
                Type = "StoreDeliveryStatusUpdated",
                RelatedEntityId = orderId,
                RelatedEntityType = "StoreOrder",
                ActionUrl = $"{await GetMemberPortalUrlAsync()}/store/orders",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

            logger.LogInformation("Dispatched StoreDeliveryStatusUpdated ({Status}) to member {MemberId} for order {OrderId}", newStatus, memberId, orderId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch StoreDeliveryStatusUpdated for order {OrderId}", orderId);
        }
    }

    public async Task DispatchBroadcastAsync(List<BroadcastRecipient> recipients, string? title, string message, List<string> channels)
    {
        try
        {
            if (recipients.Count == 0) return;

            if (channels.Contains("InApp", StringComparer.OrdinalIgnoreCase))
            {
                var portalUrl = await GetMemberPortalUrlAsync();
                var notifications = recipients.Select(r => new Notification
                {
                    RecipientId = r.Id,
                    RecipientType = "Member",
                    Title = string.IsNullOrWhiteSpace(title) ? "Announcement" : title,
                    Body = message,
                    Type = "Broadcast",
                    ActionUrl = $"{portalUrl}/notifications",
                    CreatedBy = "system",
                }).ToList();

                await notifRepo.AddRangeAsync(notifications);
            }

            if (channels.Contains("Sms", StringComparer.OrdinalIgnoreCase))
            {
                var institutionName = await GetInstitutionNameAsync();
                var smsMessage = string.IsNullOrWhiteSpace(institutionName) ? message : $"{institutionName}: {message}";

                // Broadcasts override each member's individual SMS opt-in by
                // design — an emergency/announcement notice should reach
                // everyone with a phone number on file, unlike transactional
                // notifications which respect NotificationPreference.SmsAlerts.
                foreach (var r in recipients.Where(r => !string.IsNullOrWhiteSpace(r.Phone)))
                {
                    await smsService.SendSmsAsync(r.Phone!, smsMessage);
                }
            }

            logger.LogInformation("Dispatched broadcast to {Count} recipients via [{Channels}]", recipients.Count, string.Join(",", channels));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch broadcast to {Count} recipients", recipients.Count);
        }
    }
}
