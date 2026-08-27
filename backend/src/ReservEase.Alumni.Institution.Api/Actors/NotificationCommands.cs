using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Institution.Api.Actors;

/// <summary>
/// Sends one email off the HTTP request path. Unlike NotificationCommand, this
/// carries no InstitutionId and needs none — the caller resolves any
/// tenant-branded content (brand name/color/logo) in its own request-scoped
/// GetBrandVarsAsync() before building Request, so the actor's job is purely
/// "make the outbound HTTP call to Mailtrap, catch and log any failure, never
/// let it surface back to the caller."
/// </summary>
public sealed record SendEmailCommand(SendEmailRequest Request, string Context);

/// <summary>
/// Marker base for all notification dispatch commands. Every command carries the
/// originating InstitutionId — the actor that handles it runs in a fresh DI scope
/// with no HttpContext, so ICurrentTenantService can't resolve the tenant on its
/// own the way it does for a normal request; the actor must call
/// ICurrentTenantService.SetInstitutionId(cmd.InstitutionId) itself before doing
/// any tenant-scoped work, using this value.
/// </summary>
public abstract record NotificationCommand(string InstitutionId);

public sealed record DispatchJobAlertCommand(string InstitutionId, Job Job) : NotificationCommand(InstitutionId);

public sealed record DispatchCampaignAlertCommand(string InstitutionId, Campaign Campaign) : NotificationCommand(InstitutionId);

public sealed record DispatchEventReminderCommand(string InstitutionId, AlumniEvent Event) : NotificationCommand(InstitutionId);

public sealed record DispatchSpotlightAlertCommand(string InstitutionId, Spotlight Spotlight) : NotificationCommand(InstitutionId);

public sealed record DispatchContributionConfirmedCommand(
    string InstitutionId,
    string MemberId,
    string MemberEmail,
    string MemberFirstName,
    decimal Amount,
    string CampaignTitle,
    string ContributionId) : NotificationCommand(InstitutionId);

public sealed record DispatchContributionRejectedCommand(
    string InstitutionId,
    string MemberId,
    string MemberEmail,
    string MemberFirstName,
    string CampaignTitle,
    string? Reason,
    string ContributionId) : NotificationCommand(InstitutionId);

public sealed record SendBroadcastCommand(
    string InstitutionId,
    List<BroadcastRecipient> Recipients,
    string? Title,
    string Message,
    List<string> Channels) : NotificationCommand(InstitutionId);
