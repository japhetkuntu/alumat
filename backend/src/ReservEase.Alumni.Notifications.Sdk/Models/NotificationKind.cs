namespace ReservEase.Alumni.Notifications.Sdk.Models;

/// <summary>Discriminates the one flat NotificationRequest envelope into what
/// NotificationDispatchWorkflow.ProcessOneAsync actually does with it.</summary>
public enum NotificationKind
{
    JobAlert,
    CampaignAlert,
    EventReminder,
    SpotlightAlert,
    PaymentReceivedToAdmins,
    ContributionConfirmed,
    ContributionRejected,
    MentorProfileDecision,
    StoreDeliveryStatusUpdated,
    ServiceRequestUpdated,
    Broadcast,
    ClassNoteAlert,
    MentorshipRequestReceived,
    MentorshipRequestDecision,
    ForumReply,
    MemberStatusChanged,
    NewMemberPendingApproval,
    ReferralRegistered,
    EventRsvpConfirmed,
    SpotlightDecision,
    BirthdayShoutout,
    Email,
    EventCancelled,
    EventDetailsChanged,
    EventRsvpCancelled,
}
