using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

/// <summary>Dispatches in-app notifications and emails triggered by member actions.</summary>
public interface INotificationDispatcher
{
    /// <summary>Notify year-group members (excluding author) when a class note is posted.</summary>
    Task DispatchClassNoteAlertAsync(ClassNote note, string authorName);

    /// <summary>Notify all admins when a manual contribution proof is uploaded.</summary>
    Task DispatchPaymentReceivedToAdminsAsync(string memberName, string memberEmail, decimal amount, string campaignTitle, string contributionId);

    /// <summary>Notify the member that their contribution was confirmed.</summary>
    Task DispatchContributionConfirmedAsync(string memberId, string memberEmail, string memberFirstName, decimal amount, string campaignTitle, string contributionId);

    /// <summary>Notify the member that their contribution was rejected.</summary>
    Task DispatchContributionRejectedAsync(string memberId, string memberEmail, string memberFirstName, string campaignTitle, string? reason, string contributionId);

    /// <summary>Notify a mentor that they've received a new mentorship pairing request. Always delivered — personal/transactional, not preference-gated.</summary>
    Task DispatchMentorshipRequestReceivedAsync(string mentorMemberId, string menteeName, string area, string requestId);

    /// <summary>Notify a mentee of the mentor's decision on their pairing request. Always delivered — personal/transactional, not preference-gated.</summary>
    Task DispatchMentorshipRequestDecisionAsync(string menteeId, bool accepted, string area, string requestId);

    /// <summary>Notify a thread's original author when someone else replies to it. Always delivered — personal/transactional, not preference-gated.</summary>
    Task DispatchForumReplyAsync(string threadAuthorId, string replierName, string threadTitle, string threadId);
}
