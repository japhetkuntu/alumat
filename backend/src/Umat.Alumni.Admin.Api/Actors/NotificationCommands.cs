using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace Umat.Alumni.Admin.Api.Actors;

/// <summary>Marker base for all notification dispatch commands.</summary>
public abstract record NotificationCommand;

public sealed record DispatchJobAlertCommand(Job Job) : NotificationCommand;

public sealed record DispatchCampaignAlertCommand(Campaign Campaign) : NotificationCommand;

public sealed record DispatchEventReminderCommand(AlumniEvent Event) : NotificationCommand;

public sealed record DispatchSpotlightAlertCommand(Spotlight Spotlight) : NotificationCommand;

public sealed record DispatchContributionConfirmedCommand(
    string MemberId,
    string MemberEmail,
    string MemberFirstName,
    decimal Amount,
    string CampaignTitle,
    string ContributionId) : NotificationCommand;

public sealed record DispatchContributionRejectedCommand(
    string MemberId,
    string MemberEmail,
    string MemberFirstName,
    string CampaignTitle,
    string? Reason,
    string ContributionId) : NotificationCommand;
