using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Sms.Sdk.Services;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class NotificationDispatcher(
    IAlumniPgRepository<Notification> notifRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<AdminNotificationPreference> adminPrefRepo,
    IAlumniPgRepository<StaffEntity> adminRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ICurrentTenantService currentTenant,
    ISmsService smsService,
    IWhatsAppService whatsAppService,
    IConfiguration configuration,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    /// <summary>
    /// WhatsApp is wired up end to end but switched off for the pilot — SMS
    /// and email only for now. Flip this back on (and re-surface the
    /// WhatsApp toggle in Member Portal settings) when it's ready to launch.
    /// </summary>
    private const bool WhatsAppEnabled = false;

    /// <summary>
    /// The current institution, fetched once per dispatch and reused for the
    /// SMS name-prefix, the member portal URL, and the admin portal URL below
    /// — avoids querying it once per recipient in a batch fan-out.
    /// </summary>
    private Institution? cachedInstitution;
    private bool institutionLoaded;
    private async Task<Institution?> GetInstitutionAsync()
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
    /// configured base domain instead of a hardcoded localhost literal.
    /// Falls back to an empty string if the domain isn't configured, rather
    /// than ever pointing a notification at a dev-only address in production.
    /// </summary>
    private async Task<string> GetMemberPortalUrlAsync()
    {
        var institution = await GetInstitutionAsync();
        var domain = configuration["PlatformBaseDomain"];
        return institution is null || string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{institution.Slug}.{domain}";
    }

    /// <summary>
    /// Institution admins get linked to their own institution's admin portal,
    /// which lives on the separate "admin." subdomain — see AdminBaseDomain.
    /// </summary>
    private async Task<string> GetAdminPortalUrlAsync()
    {
        var institution = await GetInstitutionAsync();
        var domain = configuration["AdminBaseDomain"];
        return institution is null || string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{institution.Slug}.{domain}";
    }

    /// <summary>
    /// Fires SMS/WhatsApp for a member if they've opted in and have a phone
    /// number on file. Both send methods already swallow their own failures
    /// (log + return false) so a gateway outage never breaks the caller's
    /// in-app/email notification flow.
    /// </summary>
    private async Task SendExternalAlertsAsync(MemberEntity member, NotificationPreference? pref, string message)
    {
        if (string.IsNullOrWhiteSpace(member.Phone) || pref is null) return;

        if (pref.SmsAlerts)
        {
            var institutionName = await GetInstitutionNameAsync();
            var smsMessage = string.IsNullOrWhiteSpace(institutionName) ? message : $"{institutionName}: {message}";
            await smsService.SendSmsAsync(member.Phone, smsMessage);
        }

        if (WhatsAppEnabled && pref.WhatsAppAlerts)
            await whatsAppService.SendMessageAsync(member.Phone, message);
    }

    public async Task DispatchClassNoteAlertAsync(ClassNote note, string authorName)
    {
        try
        {
            // Treat absent preference rows as defaults (all alerts = true).
            // Only exclude members who have explicitly opted out (or are the author).
            var optedOut = await prefRepo.GetAllAsync(p => !p.ClassNoteAlerts || p.MemberId == note.AuthorId);
            var optedOutIds = optedOut.Select(p => p.MemberId).ToHashSet();
            // Also always exclude the author even if they have no pref row
            optedOutIds.Add(note.AuthorId);
            var members = (await memberRepo.GetAllAsync(m =>
                !optedOutIds.Contains(m.Id)
                && m.Status == "Active"
                && m.GraduationYear == note.YearGroup)).ToList();

            var memberIds = members.Select(m => m.Id).ToHashSet();
            var prefsById = (await prefRepo.GetAllAsync(p => memberIds.Contains(p.MemberId)))
                .ToDictionary(p => p.MemberId);

            var alertMessage = $"{authorName} posted a note to the Class of {note.YearGroup} wall.";
            foreach (var m in members)
                await SendExternalAlertsAsync(m, prefsById.GetValueOrDefault(m.Id), alertMessage);

            var portalUrl = await GetMemberPortalUrlAsync();
            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Class Note",
                Body = $"{authorName} posted a note to the Class of {note.YearGroup} wall.",
                Type = "ClassNoteAlert",
                RelatedEntityId = note.Id,
                RelatedEntityType = "ClassNote",
                ActionUrl = $"{portalUrl}/class-notes",
                CreatedBy = "system",
            }).ToList();

            if (notifications.Count > 0)
                await notifRepo.AddRangeAsync(notifications);

            logger.LogInformation("Dispatched ClassNoteAlert for note {NoteId} to {Count} members in year {YearGroup}", note.Id, notifications.Count, note.YearGroup);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch ClassNoteAlert for note {NoteId}", note.Id);
        }
    }

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

            logger.LogInformation("Dispatched PaymentReceived notification for contribution {ContributionId} to {Count} admins", contributionId, admins.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch PaymentReceived notification for contribution {ContributionId}", contributionId);
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

            logger.LogInformation("Dispatched ContributionConfirmed notification to member {MemberId}", memberId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch ContributionConfirmed for contribution {ContributionId}", contributionId);
        }
    }

    public async Task DispatchMentorshipRequestReceivedAsync(string mentorMemberId, string menteeName, string area, string requestId)
    {
        try
        {
            var notification = new Notification
            {
                RecipientId = mentorMemberId,
                RecipientType = "Member",
                Title = "New Mentorship Request",
                Body = $"{menteeName} has requested mentorship from you in {area}.",
                Type = "MentorshipRequestReceived",
                RelatedEntityId = requestId,
                RelatedEntityType = "MentorshipRequest",
                ActionUrl = $"{await GetMemberPortalUrlAsync()}/mentorship",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

            logger.LogInformation("Dispatched MentorshipRequestReceived to mentor {MentorMemberId} for request {RequestId}", mentorMemberId, requestId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch MentorshipRequestReceived for request {RequestId}", requestId);
        }
    }

    public async Task DispatchMentorshipRequestDecisionAsync(string menteeId, bool accepted, string area, string requestId)
    {
        try
        {
            var notification = new Notification
            {
                RecipientId = menteeId,
                RecipientType = "Member",
                Title = accepted ? "Mentorship Request Accepted" : "Mentorship Request Declined",
                Body = accepted
                    ? $"Your mentorship request in {area} was accepted. You can now connect with your mentor."
                    : $"Your mentorship request in {area} was declined.",
                Type = "MentorshipRequestDecision",
                RelatedEntityId = requestId,
                RelatedEntityType = "MentorshipRequest",
                ActionUrl = $"{await GetMemberPortalUrlAsync()}/mentorship",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

            logger.LogInformation("Dispatched MentorshipRequestDecision ({Accepted}) to mentee {MenteeId} for request {RequestId}", accepted, menteeId, requestId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch MentorshipRequestDecision for request {RequestId}", requestId);
        }
    }

    public async Task DispatchForumReplyAsync(string threadAuthorId, string replierName, string threadTitle, string threadId)
    {
        try
        {
            var notification = new Notification
            {
                RecipientId = threadAuthorId,
                RecipientType = "Member",
                Title = "New Reply to Your Thread",
                Body = $"{replierName} replied to \"{threadTitle}\".",
                Type = "ForumReply",
                RelatedEntityId = threadId,
                RelatedEntityType = "ForumThread",
                ActionUrl = $"{await GetMemberPortalUrlAsync()}/forum/{threadId}",
                CreatedBy = "system",
            };
            await notifRepo.AddAsync(notification);

            logger.LogInformation("Dispatched ForumReply to thread author {ThreadAuthorId} for thread {ThreadId}", threadAuthorId, threadId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch ForumReply for thread {ThreadId}", threadId);
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
                Title = "Contribution Rejected",
                Body = body,
                Type = "ContributionRejected",
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
                await SendExternalAlertsAsync(member, pref, body);
            }

            logger.LogInformation("Dispatched ContributionRejected notification to member {MemberId}", memberId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch ContributionRejected for contribution {ContributionId}", contributionId);
        }
    }
}
