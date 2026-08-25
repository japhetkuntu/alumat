using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Member.Api.Actors;

/// <summary>Marker base for all notification dispatch commands.</summary>
public abstract record NotificationCommand;

public sealed record DispatchClassNoteAlertCommand(ClassNote Note, string AuthorName) : NotificationCommand;

public sealed record DispatchPaymentReceivedCommand(
    string MemberName,
    string MemberEmail,
    decimal Amount,
    string CampaignTitle,
    string ContributionId) : NotificationCommand;

public sealed record DispatchContributionConfirmedCommand(
    string MemberId,
    string MemberEmail,
    string MemberFirstName,
    decimal Amount,
    string CampaignTitle,
    string ContributionId) : NotificationCommand;
