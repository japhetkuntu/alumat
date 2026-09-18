using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Notifications;

/// <summary>
/// Durable processing of exactly one notification — a short-lived, request-scoped
/// workflow execution (a fresh, uniquely-IDed run per call, see
/// NotificationClientExtensions.EnqueueNotificationAsync), the same shape as every other
/// workflow in this codebase (ProcessContributionCallbackWorkflow and friends). This
/// replaces an earlier version that ran as one perpetual global queue instance — that
/// design meant any change to this class's own branching logic broke Temporal's replay
/// of whatever execution happened to still be open at deploy time (TMPRL1100
/// nondeterminism errors). A short-lived execution never stays open long enough for that
/// to matter, at the cost of no longer guaranteeing strict global ordering across every
/// notification platform-wide — nothing in this domain relies on that.
/// </summary>
[Workflow("NotificationDispatch")]
public class NotificationDispatchWorkflow : INotificationDispatchWorkflow
{
    /// <summary>WhatsApp is wired end to end but switched off for the pilot, matching
    /// PaymentCallbacks.Sdk's retired NotificationDispatcher.</summary>
    private const bool WhatsAppEnabled = false;

    [WorkflowRun]
    public Task RunAsync(NotificationRequest request) => ProcessOneAsync(request);

    private Task ProcessOneAsync(NotificationRequest request) => request.Kind switch
    {
        NotificationKind.JobAlert => ProcessJobAlertAsync(request),
        NotificationKind.CampaignAlert => ProcessCampaignAlertAsync(request),
        NotificationKind.EventReminder => ProcessEventReminderAsync(request),
        NotificationKind.SpotlightAlert => ProcessSpotlightAlertAsync(request),
        NotificationKind.PaymentReceivedToAdmins => ProcessPaymentReceivedToAdminsAsync(request),
        NotificationKind.ContributionConfirmed => ProcessContributionConfirmedAsync(request),
        NotificationKind.ContributionRejected => ProcessContributionRejectedAsync(request),
        NotificationKind.MentorProfileDecision => ProcessMentorProfileDecisionAsync(request),
        NotificationKind.StoreDeliveryStatusUpdated => ProcessStoreDeliveryStatusUpdatedAsync(request),
        NotificationKind.ServiceRequestUpdated => ProcessServiceRequestUpdatedAsync(request),
        NotificationKind.Broadcast => ProcessBroadcastAsync(request),
        NotificationKind.ClassNoteAlert => ProcessClassNoteAlertAsync(request),
        NotificationKind.MentorshipRequestReceived => ProcessMentorshipRequestReceivedAsync(request),
        NotificationKind.MentorshipRequestDecision => ProcessMentorshipRequestDecisionAsync(request),
        NotificationKind.ForumReply => ProcessForumReplyAsync(request),
        NotificationKind.MemberStatusChanged => ProcessMemberStatusChangedAsync(request),
        NotificationKind.NewMemberPendingApproval => ProcessNewMemberPendingApprovalAsync(request),
        NotificationKind.ReferralRegistered => ProcessReferralRegisteredAsync(request),
        NotificationKind.EventRsvpConfirmed => ProcessEventRsvpConfirmedAsync(request),
        NotificationKind.SpotlightDecision => ProcessSpotlightDecisionAsync(request),
        NotificationKind.BirthdayShoutout => ProcessBirthdayShoutoutAsync(request),
        NotificationKind.Email => ProcessEmailAsync(request),
        _ => LogUnhandledKindAsync(request),
    };

    private Task LogUnhandledKindAsync(NotificationRequest request)
    {
        Workflow.Logger.LogWarning("Unhandled notification kind {Kind}", request.Kind);
        return Task.CompletedTask;
    }

    /// <summary>Fires SMS/WhatsApp for a recipient who opted in and has a phone on file —
    /// mirrors both retired dispatchers' SendExternalAlertsAsync.</summary>
    private static async Task SendExternalAlertsAsync(string? phone, bool smsAllowed, bool whatsAppAllowed, bool institutionSmsEnabled, string? institutionName, string message)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;

        var prefixedMessage = string.IsNullOrWhiteSpace(institutionName) ? message : $"{institutionName}: {message}";

        if (smsAllowed && institutionSmsEnabled)
            await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.SendSmsAsync(phone, prefixedMessage), NotificationActivityOptions.ExternalGateway);

        if (WhatsAppEnabled && whatsAppAllowed)
            await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.SendWhatsAppAsync(phone, message), NotificationActivityOptions.ExternalGateway);
    }

    private async Task ProcessJobAlertAsync(NotificationRequest request)
    {
        var job = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadJobContentAsync(request.JobId!), NotificationActivityOptions.DatabaseRead);
        if (job is null) return;

        var recipients = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolveJobAlertRecipientsAsync(request.InstitutionId, job.YearGroups),
            NotificationActivityOptions.DatabaseRead);
        if (recipients.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var actionUrl = MemberUrl(institution, $"/jobs/{request.JobId}");

        var notifications = recipients.Select(r => new Notification
        {
            RecipientId = r.MemberId,
            RecipientType = "Member",
            Title = "New Job Posting",
            Body = $"{job.Title} at {job.Company} — {job.Location}",
            Type = "JobAlert",
            RelatedEntityId = request.JobId,
            RelatedEntityType = "Job",
            ActionUrl = actionUrl,
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessCampaignAlertAsync(NotificationRequest request)
    {
        var campaign = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadCampaignContentAsync(request.CampaignId!), NotificationActivityOptions.DatabaseRead);
        if (campaign is null) return;

        var recipients = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolveCampaignAlertRecipientsAsync(request.InstitutionId, campaign.YearGroups),
            NotificationActivityOptions.DatabaseRead);
        if (recipients.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var actionUrl = MemberUrl(institution, "/contributions");

        var notifications = recipients.Select(r => new Notification
        {
            RecipientId = r.MemberId,
            RecipientType = "Member",
            Title = "New Campaign Launched",
            Body = campaign.Title,
            Type = "CampaignAlert",
            RelatedEntityId = request.CampaignId,
            RelatedEntityType = "Campaign",
            ActionUrl = actionUrl,
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessEventReminderAsync(NotificationRequest request)
    {
        var ev = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadEventContentAsync(request.EventId!), NotificationActivityOptions.DatabaseRead);
        if (ev is null) return;

        var recipients = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolveEventReminderRecipientsAsync(request.InstitutionId, ev.YearGroups),
            NotificationActivityOptions.DatabaseRead);
        if (recipients.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var actionUrl = MemberUrl(institution, $"/events/{request.EventId}");
        var dateStr = ev.StartDate.ToString("dddd, MMMM d yyyy");

        var notifications = recipients.Select(r => new Notification
        {
            RecipientId = r.MemberId,
            RecipientType = "Member",
            Title = "Upcoming Event",
            Body = $"{ev.Title} — {dateStr} at {ev.Venue}",
            Type = "EventReminder",
            RelatedEntityId = request.EventId,
            RelatedEntityType = "Event",
            ActionUrl = actionUrl,
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessSpotlightAlertAsync(NotificationRequest request)
    {
        var spotlight = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadSpotlightContentAsync(request.SpotlightId!), NotificationActivityOptions.DatabaseRead);
        if (spotlight is null) return;

        var recipients = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolveSpotlightAlertRecipientsAsync(request.InstitutionId),
            NotificationActivityOptions.DatabaseRead);
        if (recipients.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var actionUrl = MemberUrl(institution, "/spotlights");
        var memberName = !string.IsNullOrWhiteSpace(spotlight.MemberFirstName) ? $"{spotlight.MemberFirstName} {spotlight.MemberLastName}" : "An alumnus";

        var notifications = recipients.Select(r => new Notification
        {
            RecipientId = r.MemberId,
            RecipientType = "Member",
            Title = "New Alumni Spotlight",
            Body = $"{memberName} — {spotlight.Title}",
            Type = "SpotlightUpdate",
            RelatedEntityId = request.SpotlightId,
            RelatedEntityType = "Spotlight",
            ActionUrl = actionUrl,
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessPaymentReceivedToAdminsAsync(NotificationRequest request)
    {
        var adminIds = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolvePaymentReceivedAdminRecipientsAsync(request.InstitutionId),
            NotificationActivityOptions.DatabaseRead);
        if (adminIds.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var actionUrl = AdminUrl(institution, "/contributions");
        var body = $"{request.MemberName} ({request.MemberEmail}) submitted a payment of GHS {request.Amount:N2} for the campaign \"{request.CampaignTitle}\". Please review and confirm.";

        var notifications = adminIds.Select(id => new Notification
        {
            RecipientId = id,
            RecipientType = "Admin",
            Title = "Payment Submitted",
            Body = body,
            Type = "PaymentReceived",
            RelatedEntityId = request.ContributionId,
            RelatedEntityType = "Contribution",
            ActionUrl = actionUrl,
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessContributionConfirmedAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var body = string.IsNullOrWhiteSpace(request.MemberFirstName)
            ? $"Your payment of GHS {request.Amount:N2} for \"{request.CampaignTitle}\" has been received. Thank you!"
            : $"Thank you, {request.MemberFirstName} — your payment of GHS {request.Amount:N2} for \"{request.CampaignTitle}\" has been received.";

        var notification = new Notification
        {
            RecipientId = request.MemberId!,
            RecipientType = "Member",
            Title = "Contribution Confirmed",
            Body = body,
            Type = "ContributionConfirmed",
            RelatedEntityId = request.ContributionId,
            RelatedEntityType = "Contribution",
            ActionUrl = MemberUrl(institution, "/contributions"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);

        await SendMemberExternalAlertsIfEligibleAsync(request.MemberId!, institution, body);
    }

    private async Task ProcessContributionRejectedAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var body = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Your contribution for \"{request.CampaignTitle}\" was not confirmed. Please contact support or resubmit."
            : $"Your contribution for \"{request.CampaignTitle}\" was not confirmed. Reason: {request.Reason}. Please resubmit or contact support.";

        var notification = new Notification
        {
            RecipientId = request.MemberId!,
            RecipientType = "Member",
            Title = "Contribution Not Confirmed",
            Body = body,
            Type = "ContributionRejected",
            RelatedEntityId = request.ContributionId,
            RelatedEntityType = "Contribution",
            ActionUrl = MemberUrl(institution, "/contributions"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);

        // Reconciled behavior: both retired dispatchers now agree contribution-rejected
        // also gets an external alert, matching contribution-confirmed's treatment
        // (only one of the two previously did this).
        await SendMemberExternalAlertsIfEligibleAsync(request.MemberId!, institution, body);
    }

    private async Task ProcessMentorProfileDecisionAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.MemberId!,
            RecipientType = "Member",
            Title = request.Approved == true ? "Mentor Application Approved" : "Mentor Application Not Approved",
            Body = request.Approved == true
                ? "Congratulations — your mentor application has been approved. You can now receive mentorship requests."
                : "Your mentor application was not approved this time.",
            Type = "MentorProfileDecision",
            RelatedEntityId = request.ProfileId,
            RelatedEntityType = "MentorProfile",
            ActionUrl = MemberUrl(institution, "/mentorship"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessStoreDeliveryStatusUpdatedAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.MemberId!,
            RecipientType = "Member",
            Title = "Order Delivery Update",
            Body = $"Your order #{request.OrderNumber} is now \"{request.NewStatus}\".",
            Type = "StoreDeliveryStatusUpdated",
            RelatedEntityId = request.OrderId,
            RelatedEntityType = "StoreOrder",
            ActionUrl = MemberUrl(institution, "/store/orders"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessServiceRequestUpdatedAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.MemberId!,
            RecipientType = "Member",
            Title = "Service Request Update",
            Body = $"Your \"{request.ServiceTypeName}\" request #{request.RequestNumber} is now \"{request.NewStage}\".",
            Type = "ServiceRequestUpdated",
            RelatedEntityId = request.RequestId,
            RelatedEntityType = "ServiceRequest",
            ActionUrl = MemberUrl(institution, "/services/requests"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessBroadcastAsync(NotificationRequest request)
    {
        var recipients = request.Recipients ?? [];
        if (recipients.Count == 0) return;

        var channels = request.Channels ?? [];
        if (channels.Contains("InApp", StringComparer.OrdinalIgnoreCase))
        {
            var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
            var notifications = recipients.Select(r => new Notification
            {
                RecipientId = r.Id,
                RecipientType = "Member",
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Announcement" : request.Title,
                Body = request.Message!,
                Type = "Broadcast",
                ActionUrl = MemberUrl(institution, "/notifications"),
                CreatedBy = "system",
            }).ToList();
            await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
        }

        if (channels.Contains("Sms", StringComparer.OrdinalIgnoreCase))
        {
            var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
            if (institution?.SmsNotificationsEnabled != false)
            {
                var smsMessage = string.IsNullOrWhiteSpace(institution?.Name) ? request.Message! : $"{institution.Name}: {request.Message}";
                // Broadcasts override each member's individual SMS opt-in by design — an
                // emergency/announcement notice reaches everyone with a phone on file,
                // unlike transactional notifications which respect NotificationPreference.SmsAlerts.
                foreach (var r in recipients.Where(r => !string.IsNullOrWhiteSpace(r.Phone)))
                    await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.SendSmsAsync(r.Phone!, smsMessage), NotificationActivityOptions.ExternalGateway);
            }
        }
    }

    private async Task ProcessClassNoteAlertAsync(NotificationRequest request)
    {
        var note = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadClassNoteContentAsync(request.NoteId!), NotificationActivityOptions.DatabaseRead);
        if (note is null) return;

        var recipients = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolveClassNoteAlertRecipientsAsync(request.InstitutionId, note.YearGroup, note.AuthorId),
            NotificationActivityOptions.DatabaseRead);
        if (recipients.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var body = $"{request.AuthorName} posted a note to the Class of {note.YearGroup} wall.";

        var notifications = recipients.Select(r => new Notification
        {
            RecipientId = r.MemberId,
            RecipientType = "Member",
            Title = "New Class Note",
            Body = body,
            Type = "ClassNoteAlert",
            RelatedEntityId = request.NoteId,
            RelatedEntityType = "ClassNote",
            ActionUrl = MemberUrl(institution, "/class-notes"),
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);

        foreach (var r in recipients)
            await SendExternalAlertsAsync(r.Phone, r.SmsAlerts, r.WhatsAppAlerts, institution?.SmsNotificationsEnabled != false, institution?.Name, body);
    }

    private async Task ProcessMentorshipRequestReceivedAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.MentorMemberId!,
            RecipientType = "Member",
            Title = "New Mentorship Request",
            Body = $"{request.MenteeName} has requested mentorship from you in {request.Area}.",
            Type = "MentorshipRequestReceived",
            RelatedEntityId = request.RequestId,
            RelatedEntityType = "MentorshipRequest",
            ActionUrl = MemberUrl(institution, "/mentorship"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessMentorshipRequestDecisionAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.MenteeId!,
            RecipientType = "Member",
            Title = request.Accepted == true ? "Mentorship Request Accepted" : "Mentorship Request Declined",
            Body = request.Accepted == true
                ? $"Your mentorship request in {request.Area} was accepted. You can now connect with your mentor."
                : $"Your mentorship request in {request.Area} was declined.",
            Type = "MentorshipRequestDecision",
            RelatedEntityId = request.RequestId,
            RelatedEntityType = "MentorshipRequest",
            ActionUrl = MemberUrl(institution, "/mentorship"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessForumReplyAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.ThreadAuthorId!,
            RecipientType = "Member",
            Title = "New Reply to Your Thread",
            Body = $"{request.ReplierName} replied to \"{request.ThreadTitle}\".",
            Type = "ForumReply",
            RelatedEntityId = request.ThreadId,
            RelatedEntityType = "ForumThread",
            ActionUrl = MemberUrl(institution, $"/forum/{request.ThreadId}"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    /// <summary>
    /// Only members whose new status is "Active" (approved, or unbanned) can
    /// actually log in to see an in-app row — everyone else (rejected/
    /// blocked/banned) is reached by SMS/WhatsApp only here; the caller
    /// (MemberManagementService) separately sends an email for every
    /// transition, since email is the one channel that reaches a member
    /// regardless of whether they can currently sign in.
    /// </summary>
    private async Task ProcessMemberStatusChangedAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var (title, body) = request.NewStatus switch
        {
            "Active" => ("Welcome — You're Approved!", string.IsNullOrWhiteSpace(request.MemberFirstName)
                ? "Your membership has been approved. Welcome aboard!"
                : $"Welcome, {request.MemberFirstName} — your membership has been approved. You now have full access to the portal."),
            "Suspended" => ("Registration Not Approved", string.IsNullOrWhiteSpace(request.Reason)
                ? "Your registration was not approved this time. You're welcome to register again."
                : $"Your registration was not approved. Reason: {request.Reason}. You're welcome to register again."),
            "Blocked" => ("Registration Blocked", "Your registration was not approved after multiple attempts, and this email address can no longer be used to register."),
            "Banned" => ("Account Suspended", string.IsNullOrWhiteSpace(request.Reason)
                ? "Your account has been suspended. Please contact your institution for details."
                : $"Your account has been suspended. Reason: {request.Reason}."),
            _ => ("Account Status Updated", "Your account status has been updated."),
        };

        if (request.NewStatus == "Active")
        {
            var notification = new Notification
            {
                RecipientId = request.MemberId!,
                RecipientType = "Member",
                Title = title,
                Body = body,
                Type = "MemberStatusChanged",
                RelatedEntityId = request.MemberId,
                RelatedEntityType = "Member",
                ActionUrl = MemberUrl(institution, "/"),
                CreatedBy = "system",
            };
            await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
        }

        await SendMemberExternalAlertsIfEligibleAsync(request.MemberId!, institution, body);
    }

    private async Task ProcessNewMemberPendingApprovalAsync(NotificationRequest request)
    {
        var adminIds = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolvePendingApprovalAdminRecipientsAsync(request.InstitutionId),
            NotificationActivityOptions.DatabaseRead);
        if (adminIds.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var actionUrl = AdminUrl(institution, "/members");

        var notifications = adminIds.Select(id => new Notification
        {
            RecipientId = id,
            RecipientType = "Admin",
            Title = "New Member Awaiting Approval",
            Body = $"{request.MemberName} ({request.MemberEmail}) registered and is waiting for approval.",
            Type = "NewMemberPendingApproval",
            RelatedEntityId = request.MemberId,
            RelatedEntityType = "Member",
            ActionUrl = actionUrl,
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessReferralRegisteredAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.ReferrerId!,
            RecipientType = "Member",
            Title = "Your Referral Joined!",
            Body = $"{request.ReferredName} signed up using your referral link.",
            Type = "ReferralRegistered",
            RelatedEntityType = "Referral",
            ActionUrl = MemberUrl(institution, "/referrals"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessEventRsvpConfirmedAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.MemberId!,
            RecipientType = "Member",
            Title = "RSVP Confirmed",
            Body = $"You're confirmed for \"{request.EventTitle}\". See you there!",
            Type = "EventRsvpConfirmed",
            RelatedEntityId = request.EventId,
            RelatedEntityType = "Event",
            ActionUrl = MemberUrl(institution, $"/events/{request.EventId}"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessSpotlightDecisionAsync(NotificationRequest request)
    {
        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var notification = new Notification
        {
            RecipientId = request.MemberId!,
            RecipientType = "Member",
            Title = request.Approved == true ? "Your Spotlight Was Approved!" : "Spotlight Not Approved",
            Body = request.Approved == true
                ? "Congratulations — your spotlight submission is now live for everyone to see."
                : string.IsNullOrWhiteSpace(request.Reason)
                    ? "Your spotlight submission was not approved this time."
                    : $"Your spotlight submission was not approved. Reason: {request.Reason}.",
            Type = "SpotlightDecision",
            RelatedEntityId = request.SpotlightId,
            RelatedEntityType = "Spotlight",
            ActionUrl = MemberUrl(institution, "/spotlights"),
            CreatedBy = "system",
        };
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationAsync(request.InstitutionId, notification), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessBirthdayShoutoutAsync(NotificationRequest request)
    {
        var recipients = await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.ResolveSpotlightAlertRecipientsAsync(request.InstitutionId),
            NotificationActivityOptions.DatabaseRead);
        recipients = recipients.Where(r => r.MemberId != request.MemberId).ToList();
        if (recipients.Count == 0) return;

        var institution = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadInstitutionAsync(request.InstitutionId), NotificationActivityOptions.DatabaseRead);
        var actionUrl = MemberUrl(institution, $"/forum/{request.ThreadId}");
        var body = $"It's {request.MemberName}'s birthday today! Drop by the forum and wish them well.";

        var notifications = recipients.Select(r => new Notification
        {
            RecipientId = r.MemberId,
            RecipientType = "Member",
            Title = "Birthday Shoutout",
            Body = body,
            Type = "BirthdayShoutout",
            RelatedEntityId = request.ThreadId,
            RelatedEntityType = "ForumThread",
            ActionUrl = actionUrl,
            CreatedBy = "system",
        }).ToList();
        await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.CreateNotificationsAsync(request.InstitutionId, notifications), NotificationActivityOptions.DatabaseWrite);
    }

    private async Task ProcessEmailAsync(NotificationRequest request)
    {
        await Workflow.ExecuteActivityAsync(
            (NotificationDispatchActivities a) => a.SendEmailAsync(request.EmailRequest!, request.EmailContext ?? "notification-dispatch"),
            NotificationActivityOptions.ExternalGateway);
    }

    private async Task SendMemberExternalAlertsIfEligibleAsync(string memberId, InstitutionContactInfo? institution, string message)
    {
        var member = await Workflow.ExecuteActivityAsync((NotificationDispatchActivities a) => a.LoadMemberWithPreferenceAsync(memberId), NotificationActivityOptions.DatabaseRead);
        if (member is null) return;

        await SendExternalAlertsAsync(member.Phone, member.SmsAlerts, member.WhatsAppAlerts, institution?.SmsNotificationsEnabled != false, institution?.Name, message);
    }

    private static string MemberUrl(InstitutionContactInfo? institution, string path) =>
        string.IsNullOrEmpty(institution?.MemberPortalUrl) ? string.Empty : $"{institution.MemberPortalUrl}{path}";

    private static string AdminUrl(InstitutionContactInfo? institution, string path) =>
        string.IsNullOrEmpty(institution?.AdminPortalUrl) ? string.Empty : $"{institution.AdminPortalUrl}{path}";
}
