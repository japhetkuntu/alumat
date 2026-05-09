using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Mailtrap.Sdk.Models;
using Umat.Alumni.Mailtrap.Sdk.Services;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

/// <summary>Dispatches in-app notifications and emails triggered by admin actions.</summary>
public interface INotificationDispatcher
{
    /// <summary>Fan-out job alert to all eligible members whose jobAlerts preference is on.</summary>
    Task DispatchJobAlertAsync(Job job);

    /// <summary>Fan-out campaign alert to all eligible members whose campaignAlerts preference is on.</summary>
    Task DispatchCampaignAlertAsync(Campaign campaign);

    /// <summary>Fan-out event reminder to all eligible members whose eventReminders preference is on.</summary>
    Task DispatchEventReminderAsync(AlumniEvent ev);

    /// <summary>Fan-out spotlight update to all members whose spotlightAlerts preference is on.</summary>
    Task DispatchSpotlightAlertAsync(Spotlight spotlight);

    /// <summary>Notify all admins that a contribution/payment was submitted and requires review.</summary>
    Task DispatchPaymentReceivedToAdminsAsync(string memberName, string memberEmail, decimal amount, string campaignTitle, string contributionId);

    /// <summary>Notify the member that their contribution was confirmed.</summary>
    Task DispatchContributionConfirmedAsync(string memberId, string memberEmail, string memberFirstName, decimal amount, string campaignTitle, string contributionId);

    /// <summary>Notify the member that their contribution was rejected.</summary>
    Task DispatchContributionRejectedAsync(string memberId, string memberEmail, string memberFirstName, string campaignTitle, string? reason, string contributionId);
}
