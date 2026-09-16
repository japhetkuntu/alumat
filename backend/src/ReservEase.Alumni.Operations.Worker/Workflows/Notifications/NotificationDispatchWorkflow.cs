using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Notifications;

/// <summary>
/// One perpetual global instance (see NotificationWorkflowId) acting as a durable queue —
/// the direct replacement for the Akka NotificationDispatcherActor singletons that used to
/// live in each web API project. Callers never start-and-wait: they signal-with-start
/// (NotificationClientExtensions.EnqueueNotificationAsync) and move on. ContinueAsNewSuggested
/// is checked after every processed item (not just when the queue drains) so history stays
/// bounded even under a sustained burst, carrying forward whatever's still queued.
/// </summary>
[Workflow("NotificationDispatch")]
public class NotificationDispatchWorkflow : INotificationDispatchWorkflow
{
    /// <summary>WhatsApp is wired end to end but switched off for the pilot, matching
    /// PaymentCallbacks.Sdk's retired NotificationDispatcher.</summary>
    private const bool WhatsAppEnabled = false;

    private readonly Queue<NotificationRequest> pending = new();

    [WorkflowSignal("Enqueue")]
    public Task EnqueueAsync(NotificationRequest request)
    {
        pending.Enqueue(request);
        return Task.CompletedTask;
    }

    [WorkflowRun]
    public async Task RunAsync(List<NotificationRequest>? carryOver)
    {
        foreach (var r in carryOver ?? [])
            pending.Enqueue(r);

        while (true)
        {
            await Workflow.WaitConditionAsync(() => pending.Count > 0);
            var request = pending.Dequeue();

            try
            {
                await ProcessOneAsync(request);
            }
            catch (Exception ex)
            {
                // Activities already retry via their own ActivityOptions — this is a
                // last-resort backstop so one malformed/failing item can never stall
                // every notification queued behind it.
                Workflow.Logger.LogError(ex, "Unhandled error processing notification {Kind} for institution {InstitutionId}", request.Kind, request.InstitutionId);
            }

            if (Workflow.ContinueAsNewSuggested)
            {
                throw Workflow.CreateContinueAsNewException(
                    (NotificationDispatchWorkflow wf) => wf.RunAsync(pending.ToList()),
                    new ContinueAsNewOptions());
            }
        }
    }

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
