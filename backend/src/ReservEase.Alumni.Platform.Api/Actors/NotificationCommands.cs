using ReservEase.Alumni.Mailtrap.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Actors;

/// <summary>
/// Sends one email off the HTTP request path. Platform.Api has no tenant
/// context of its own (it operates across every institution), so unlike
/// Institution.Api/Member.Api's notification commands this carries no
/// InstitutionId — the caller builds the fully-resolved Request first, and
/// the actor's only job is "make the outbound HTTP call to Mailtrap, catch
/// and log any failure, never let it surface back to the caller."
/// </summary>
public sealed record SendEmailCommand(SendEmailRequest Request, string Context);
