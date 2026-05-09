using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using AdminEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Admin;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class NotificationDispatcher(
    IAlumniPgRepository<Notification> notifRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<AdminEntity> adminRepo,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    private const string PortalBaseUrl = "http://localhost:3001";
    private const string AdminPortalBaseUrl = "http://localhost:3000";

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
            var members = await memberRepo.GetAllAsync(m =>
                !optedOutIds.Contains(m.Id)
                && m.Status == "Active"
                && m.GraduationYear == note.YearGroup);

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
                Body = $"Your payment of GHS {amount:N2} for \"{campaignTitle}\" has been confirmed. Thank you!",
                Type = "ContributionConfirmed",
                RelatedEntityId = contributionId,
                RelatedEntityType = "Contribution",
                ActionUrl = $"{PortalBaseUrl}/contributions",
                CreatedBy = "system",
            };

            await notifRepo.AddAsync(notification);

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

            logger.LogInformation("Dispatched ContributionRejected notification to member {MemberId}", memberId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to dispatch ContributionRejected for contribution {ContributionId}", contributionId);
        }
    }
}
