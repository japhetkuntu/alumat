using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Member.Api.Actors;

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

/// <summary>Deprecated feature — Class Notes are no longer surfaced anywhere in the member portal UI, this command is dead but left in place rather than touching the still-live backend entity/controller.</summary>
public sealed record DispatchClassNoteAlertCommand(string InstitutionId, ClassNote Note, string AuthorName) : NotificationCommand(InstitutionId);

public sealed record DispatchPaymentReceivedCommand(
    string InstitutionId,
    string MemberName,
    string MemberEmail,
    decimal Amount,
    string CampaignTitle,
    string ContributionId) : NotificationCommand(InstitutionId);

public sealed record DispatchContributionConfirmedCommand(
    string InstitutionId,
    string MemberId,
    string MemberEmail,
    string MemberFirstName,
    decimal Amount,
    string CampaignTitle,
    string ContributionId) : NotificationCommand(InstitutionId);
