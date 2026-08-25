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
    IAlumniPgRepository<StaffEntity> adminRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ICurrentTenantService currentTenant,
    ISmsService smsService,
    IWhatsAppService whatsAppService,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    private const string PortalBaseUrl = "http://localhost:3001";
    private const string AdminPortalBaseUrl = "http://localhost:3000";

    /// <summary>
    /// WhatsApp is wired up end to end but switched off for the pilot — SMS
    /// and email only for now. Flip this back on (and re-surface the
    /// WhatsApp toggle in Member Portal settings) when it's ready to launch.
    /// </summary>
    private const bool WhatsAppEnabled = false;

    /// <summary>
    /// Arkesel sender IDs are fixed and pre-registered per platform account —
    /// an institution's own name can't become the "from" field without a
    /// separate registration process outside this codebase — so
    /// personalization happens in the message body instead: every SMS is
    /// prefixed with the institution's name, cached per-dispatch so a batch
    /// send (e.g. a class-note alert to a whole year group) only queries it
    /// once rather than once per recipient.
    /// </summary>
    private string? cachedInstitutionName;
    private bool institutionNameLoaded;
    private async Task<string?> GetInstitutionNameAsync()
    {
        if (institutionNameLoaded) return cachedInstitutionName;
        institutionNameLoaded = true;
        if (!string.IsNullOrEmpty(currentTenant.InstitutionId))
        {
            var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            cachedInstitutionName = institution?.Name;
        }
        return cachedInstitutionName;
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

            var notifications = members.Select(m => new Notification
            {
                RecipientId = m.Id,
                RecipientType = "Member",
                Title = "New Class Note",
                Body = $"{authorName} posted a note to the Class of {note.YearGroup} wall.",
                Type = "ClassNoteAlert",
                RelatedEntityId = note.Id,
                RelatedEntityType = "ClassNote",
                ActionUrl = $"{PortalBaseUrl}/class-notes",
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
                ActionUrl = $"{PortalBaseUrl}/contributions",
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
                ActionUrl = $"{PortalBaseUrl}/contributions",
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
