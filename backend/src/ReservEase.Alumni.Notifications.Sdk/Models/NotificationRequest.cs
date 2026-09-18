using ReservEase.Alumni.Mailtrap.Sdk.Models;

namespace ReservEase.Alumni.Notifications.Sdk.Models;

/// <summary>
/// One flat envelope for every kind of notification the platform sends — the payload
/// travels over a Temporal signal (see INotificationDispatchWorkflow.EnqueueAsync), so it
/// carries entity IDs rather than full entities: NotificationDispatchWorkflow reloads
/// fresh data via its own activities rather than trusting whatever was true at signal time.
/// Wide by design (one record per Kind would need a discriminated/polymorphic payload,
/// which Temporal's default JSON conversion doesn't need here) — always build one via the
/// matching static factory below rather than setting fields by hand, so a caller can't
/// mix up which fields a given Kind actually reads.
/// </summary>
public sealed class NotificationRequest
{
    public required NotificationKind Kind { get; init; }
    public string InstitutionId { get; init; } = string.Empty;

    // Entity ids the workflow reloads fresh via activities.
    public string? JobId { get; init; }
    public string? CampaignId { get; init; }
    public string? EventId { get; init; }
    public string? SpotlightId { get; init; }
    public string? NoteId { get; init; }

    // Scalar fields already known at signal time — no reload needed.
    public string? MemberId { get; init; }
    public string? MemberEmail { get; init; }
    public string? MemberFirstName { get; init; }
    public string? MemberName { get; init; }
    public decimal? Amount { get; init; }
    public string? CampaignTitle { get; init; }
    public string? ContributionId { get; init; }
    public string? Reason { get; init; }
    public bool? Approved { get; init; }
    public string? ProfileId { get; init; }
    public string? OrderId { get; init; }
    public string? OrderNumber { get; init; }
    public string? NewStatus { get; init; }
    public string? RequestId { get; init; }
    public string? RequestNumber { get; init; }
    public string? ServiceTypeName { get; init; }
    public string? NewStage { get; init; }
    public string? AuthorName { get; init; }
    public string? MentorMemberId { get; init; }
    public string? MenteeId { get; init; }
    public string? MenteeName { get; init; }
    public string? Area { get; init; }
    public bool? Accepted { get; init; }
    public string? ThreadAuthorId { get; init; }
    public string? ReplierName { get; init; }
    public string? ThreadTitle { get; init; }
    public string? ThreadId { get; init; }
    public string? ReferrerId { get; init; }
    public string? ReferredName { get; init; }
    public string? EventTitle { get; init; }

    // Broadcast.
    public List<BroadcastRecipient>? Recipients { get; init; }
    public string? Title { get; init; }
    public string? Message { get; init; }
    public List<string>? Channels { get; init; }

    // Email.
    public SendEmailRequest? EmailRequest { get; init; }
    public string? EmailContext { get; init; }

    public static NotificationRequest JobAlert(string institutionId, string jobId) => new()
    { Kind = NotificationKind.JobAlert, InstitutionId = institutionId, JobId = jobId };

    public static NotificationRequest CampaignAlert(string institutionId, string campaignId) => new()
    { Kind = NotificationKind.CampaignAlert, InstitutionId = institutionId, CampaignId = campaignId };

    public static NotificationRequest EventReminder(string institutionId, string eventId) => new()
    { Kind = NotificationKind.EventReminder, InstitutionId = institutionId, EventId = eventId };

    public static NotificationRequest SpotlightAlert(string institutionId, string spotlightId) => new()
    { Kind = NotificationKind.SpotlightAlert, InstitutionId = institutionId, SpotlightId = spotlightId };

    public static NotificationRequest PaymentReceivedToAdmins(
        string institutionId, string memberName, string memberEmail, decimal amount, string campaignTitle, string contributionId) => new()
    {
        Kind = NotificationKind.PaymentReceivedToAdmins, InstitutionId = institutionId,
        MemberName = memberName, MemberEmail = memberEmail, Amount = amount,
        CampaignTitle = campaignTitle, ContributionId = contributionId,
    };

    public static NotificationRequest ContributionConfirmed(
        string institutionId, string memberId, string memberEmail, string memberFirstName, decimal amount, string campaignTitle, string contributionId) => new()
    {
        Kind = NotificationKind.ContributionConfirmed, InstitutionId = institutionId,
        MemberId = memberId, MemberEmail = memberEmail, MemberFirstName = memberFirstName,
        Amount = amount, CampaignTitle = campaignTitle, ContributionId = contributionId,
    };

    public static NotificationRequest ContributionRejected(
        string institutionId, string memberId, string memberEmail, string memberFirstName, string campaignTitle, string? reason, string contributionId) => new()
    {
        Kind = NotificationKind.ContributionRejected, InstitutionId = institutionId,
        MemberId = memberId, MemberEmail = memberEmail, MemberFirstName = memberFirstName,
        CampaignTitle = campaignTitle, Reason = reason, ContributionId = contributionId,
    };

    public static NotificationRequest MentorProfileDecision(string institutionId, string memberId, bool approved, string profileId) => new()
    { Kind = NotificationKind.MentorProfileDecision, InstitutionId = institutionId, MemberId = memberId, Approved = approved, ProfileId = profileId };

    public static NotificationRequest StoreDeliveryStatusUpdated(
        string institutionId, string memberId, string orderId, string orderNumber, string newStatus) => new()
    {
        Kind = NotificationKind.StoreDeliveryStatusUpdated, InstitutionId = institutionId,
        MemberId = memberId, OrderId = orderId, OrderNumber = orderNumber, NewStatus = newStatus,
    };

    public static NotificationRequest ServiceRequestUpdated(
        string institutionId, string memberId, string requestId, string requestNumber, string serviceTypeName, string newStage) => new()
    {
        Kind = NotificationKind.ServiceRequestUpdated, InstitutionId = institutionId,
        MemberId = memberId, RequestId = requestId, RequestNumber = requestNumber,
        ServiceTypeName = serviceTypeName, NewStage = newStage,
    };

    public static NotificationRequest Broadcast(string institutionId, List<BroadcastRecipient> recipients, string? title, string message, List<string> channels) => new()
    { Kind = NotificationKind.Broadcast, InstitutionId = institutionId, Recipients = recipients, Title = title, Message = message, Channels = channels };

    public static NotificationRequest ClassNoteAlert(string institutionId, string noteId, string authorName) => new()
    { Kind = NotificationKind.ClassNoteAlert, InstitutionId = institutionId, NoteId = noteId, AuthorName = authorName };

    public static NotificationRequest MentorshipRequestReceived(string institutionId, string mentorMemberId, string menteeName, string area, string requestId) => new()
    {
        Kind = NotificationKind.MentorshipRequestReceived, InstitutionId = institutionId,
        MentorMemberId = mentorMemberId, MenteeName = menteeName, Area = area, RequestId = requestId,
    };

    public static NotificationRequest MentorshipRequestDecision(string institutionId, string menteeId, bool accepted, string area, string requestId) => new()
    {
        Kind = NotificationKind.MentorshipRequestDecision, InstitutionId = institutionId,
        MenteeId = menteeId, Accepted = accepted, Area = area, RequestId = requestId,
    };

    public static NotificationRequest ForumReply(string institutionId, string threadAuthorId, string replierName, string threadTitle, string threadId) => new()
    {
        Kind = NotificationKind.ForumReply, InstitutionId = institutionId,
        ThreadAuthorId = threadAuthorId, ReplierName = replierName, ThreadTitle = threadTitle, ThreadId = threadId,
    };

    public static NotificationRequest MemberStatusChanged(
        string institutionId, string memberId, string memberFirstName, string newStatus, string? reason) => new()
    {
        Kind = NotificationKind.MemberStatusChanged, InstitutionId = institutionId,
        MemberId = memberId, MemberFirstName = memberFirstName, NewStatus = newStatus, Reason = reason,
    };

    public static NotificationRequest NewMemberPendingApproval(string institutionId, string memberId, string memberName, string memberEmail) => new()
    {
        Kind = NotificationKind.NewMemberPendingApproval, InstitutionId = institutionId,
        MemberId = memberId, MemberName = memberName, MemberEmail = memberEmail,
    };

    public static NotificationRequest ReferralRegistered(string institutionId, string referrerId, string referredName) => new()
    { Kind = NotificationKind.ReferralRegistered, InstitutionId = institutionId, ReferrerId = referrerId, ReferredName = referredName };

    public static NotificationRequest EventRsvpConfirmed(string institutionId, string memberId, string eventId, string eventTitle) => new()
    { Kind = NotificationKind.EventRsvpConfirmed, InstitutionId = institutionId, MemberId = memberId, EventId = eventId, EventTitle = eventTitle };

    public static NotificationRequest SpotlightDecision(string institutionId, string memberId, bool approved, string? reason, string spotlightId) => new()
    {
        Kind = NotificationKind.SpotlightDecision, InstitutionId = institutionId,
        MemberId = memberId, Approved = approved, Reason = reason, SpotlightId = spotlightId,
    };

    /// <summary>Fanned out to everyone except the celebrant — one call per celebrant when several members share a birthday, each pointing at that member's own shoutout thread.</summary>
    public static NotificationRequest BirthdayShoutout(string institutionId, string celebrantMemberId, string celebrantName, string threadId) => new()
    {
        Kind = NotificationKind.BirthdayShoutout, InstitutionId = institutionId,
        MemberId = celebrantMemberId, MemberName = celebrantName, ThreadId = threadId,
    };

    public static NotificationRequest Email(SendEmailRequest request, string context) => new()
    { Kind = NotificationKind.Email, EmailRequest = request, EmailContext = context };
}
