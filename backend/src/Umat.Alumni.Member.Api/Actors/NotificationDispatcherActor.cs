using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using Umat.Alumni.Member.Api.Services.Interfaces;

namespace Umat.Alumni.Member.Api.Actors;

/// <summary>
/// A singleton Akka actor that processes all notification fan-out commands.
/// Creates a fresh DI scope per message so scoped services (DbContext, repositories)
/// are properly managed and not affected by the HTTP request lifetime.
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
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchPaymentReceivedToAdminsAsync(
                    cmd.MemberName, cmd.MemberEmail, cmd.Amount, cmd.CampaignTitle, cmd.ContributionId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching PaymentReceived for contribution {0}", cmd.ContributionId);
            }
        });
    }
}
