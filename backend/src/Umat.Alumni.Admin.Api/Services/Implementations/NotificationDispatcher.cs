using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using AdminEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Admin;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class NotificationDispatcher(
    IAlumniPgRepository<Notification> notifRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<AdminEntity> adminRepo,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    private const string PortalBaseUrl = "http://localhost:3001";  // member portal
    private const string AdminPortalBaseUrl = "http://localhost:3000"; // admin portal

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

            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Job Posting",
                Body = $"{job.Title} at {job.Company} — {job.Location}",
                Type = "JobAlert",
                RelatedEntityId = job.Id,
                RelatedEntityType = "Job",
                ActionUrl = $"{PortalBaseUrl}/jobs/{job.Id}",
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

            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Campaign Launched",
                Body = campaign.Title,
                Type = "CampaignAlert",
                RelatedEntityId = campaign.Id,
                RelatedEntityType = "Campaign",
                ActionUrl = $"{PortalBaseUrl}/contributions",
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
            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "Upcoming Event",
                Body = $"{ev.Title} — {dateStr} at {ev.Venue}",
                Type = "EventReminder",
                RelatedEntityId = ev.Id,
                RelatedEntityType = "Event",
                ActionUrl = $"{PortalBaseUrl}/events/{ev.Id}",
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

            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Alumni Spotlight",
                Body = $"{memberName} — {spotlight.Title}",
                Type = "SpotlightUpdate",
                RelatedEntityId = spotlight.Id,
                RelatedEntityType = "Spotlight",
                ActionUrl = $"{PortalBaseUrl}/spotlights",
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
            var admins = (await adminRepo.GetAllAsync(a => !a.IsDisabled)).ToList();
            var body = $"{memberName} ({memberEmail}) submitted a payment of GHS {amount:N2} for the campaign \"{campaignTitle}\". Please review and confirm.";

            var notifications = admins.Select(a => new Notification
            {
                RecipientId = a.Id,
                RecipientType = "Admin",
                Title = "Payment Submitted",
                Body = body,
                Type = "PaymentReceived",
                RelatedEntityId = contributionId,
                RelatedEntityType = "Contribution",
                ActionUrl = $"{AdminPortalBaseUrl}/contributions",
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
                Body = $"Your payment of GHS {amount:N2} for \"{campaignTitle}\" has been confirmed. Thank you!",
                Type = "ContributionConfirmed",
                RelatedEntityId = contributionId,
                RelatedEntityType = "Contribution",
                ActionUrl = $"{PortalBaseUrl}/contributions",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

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
                ActionUrl = $"{PortalBaseUrl}/contributions",
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

}
