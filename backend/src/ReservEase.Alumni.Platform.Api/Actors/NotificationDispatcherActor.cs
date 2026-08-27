using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Mailtrap.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Actors;

/// <summary>
/// A singleton Akka actor that processes outbound email off the HTTP request
/// path. Creates a fresh DI scope per message so scoped services aren't
/// affected by the HTTP request lifetime. Every handler swallows its own
/// exceptions and only logs — a failed send must never crash/restart this
/// actor or propagate back to whichever fire-and-forget caller enqueued it.
/// </summary>
public class NotificationDispatcherActor : ReceiveActor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggingAdapter _log;

    public NotificationDispatcherActor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _log = Context.GetLogger();

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
