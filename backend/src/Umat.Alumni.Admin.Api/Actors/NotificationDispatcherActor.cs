using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using Umat.Alumni.Admin.Api.Services.Interfaces;

namespace Umat.Alumni.Admin.Api.Actors;

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

        ReceiveAsync<DispatchJobAlertCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchJobAlertAsync(cmd.Job);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching JobAlert for job {0}", cmd.Job.Id);
            }
        });

        ReceiveAsync<DispatchCampaignAlertCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchCampaignAlertAsync(cmd.Campaign);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching CampaignAlert for campaign {0}", cmd.Campaign.Id);
            }
        });

        ReceiveAsync<DispatchEventReminderCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchEventReminderAsync(cmd.Event);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching EventReminder for event {0}", cmd.Event.Id);
            }
        });

        ReceiveAsync<DispatchSpotlightAlertCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchSpotlightAlertAsync(cmd.Spotlight);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching SpotlightAlert for spotlight {0}", cmd.Spotlight.Id);
            }
        });

        ReceiveAsync<DispatchContributionConfirmedCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchContributionConfirmedAsync(
                    cmd.MemberId, cmd.MemberEmail, cmd.MemberFirstName,
                    cmd.Amount, cmd.CampaignTitle, cmd.ContributionId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching ContributionConfirmed for contribution {0}", cmd.ContributionId);
            }
        });

        ReceiveAsync<DispatchContributionRejectedCommand>(async cmd =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                await dispatcher.DispatchContributionRejectedAsync(
                    cmd.MemberId, cmd.MemberEmail, cmd.MemberFirstName,
                    cmd.CampaignTitle, cmd.Reason, cmd.ContributionId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error dispatching ContributionRejected for contribution {0}", cmd.ContributionId);
            }
        });
    }
}
