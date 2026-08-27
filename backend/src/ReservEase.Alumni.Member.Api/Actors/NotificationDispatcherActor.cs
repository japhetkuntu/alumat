using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Actors;

/// <summary>
/// A singleton Akka actor that processes all notification fan-out commands.
/// Creates a fresh DI scope per message so scoped services (DbContext, repositories)
/// are properly managed and not affected by the HTTP request lifetime.
///
/// Each fresh scope's ICurrentTenantService starts unset (no HttpContext ever runs
/// through this scope for TenantResolutionMiddleware to populate it) — every handler
/// must call SetInstitutionId(cmd.InstitutionId) before resolving INotificationDispatcher,
/// or every tenant-scoped query/save inside the dispatcher silently no-ops (empty
/// results) or saves with a blank InstitutionId (invisible to any real query).
/// </summary>
public class NotificationDispatcherActor : ReceiveActor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggingAdapter _log;

    public NotificationDispatcherActor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _log = Context.GetLogger();

        ReceiveAsync<DispatchClassNoteAlertCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ICurrentTenantService>().SetInstitutionId(cmd.InstitutionId);
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchClassNoteAlertAsync(cmd.Note, cmd.AuthorName);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching ClassNoteAlert for note {0}", cmd.Note.Id);
            }
        });

        ReceiveAsync<DispatchPaymentReceivedCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ICurrentTenantService>().SetInstitutionId(cmd.InstitutionId);
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchPaymentReceivedToAdminsAsync(
                    cmd.MemberName, cmd.MemberEmail, cmd.Amount, cmd.CampaignTitle, cmd.ContributionId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching PaymentReceived for contribution {0}", cmd.ContributionId);
            }
        });

        ReceiveAsync<DispatchContributionConfirmedCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ICurrentTenantService>().SetInstitutionId(cmd.InstitutionId);
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchContributionConfirmedAsync(
                    cmd.MemberId, cmd.MemberEmail, cmd.MemberFirstName, cmd.Amount, cmd.CampaignTitle, cmd.ContributionId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching ContributionConfirmed for contribution {0}", cmd.ContributionId);
            }
        });

        ReceiveAsync<SendEmailCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var result = await emailService.SendEmailAsync(cmd.Request);
                if (!result.Success)
                    _log.Error("Email not delivered ({0}): {1}", cmd.Context, result.Error);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error sending email ({0})", cmd.Context);
            }
        });
    }
}
