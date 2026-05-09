using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

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
}
