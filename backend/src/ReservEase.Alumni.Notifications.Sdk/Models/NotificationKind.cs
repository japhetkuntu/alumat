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
    /// <summary>The organiser posted an update to an event; sent to each member who is going. In-app and push only, never SMS or email.</summary>
    EventUpdatePosted,
    /// <summary>A member submitted something an administrator must decide (a listing, a suggestion, a story, a mentor profile).</summary>
    RequestAwaitingReview,
}
