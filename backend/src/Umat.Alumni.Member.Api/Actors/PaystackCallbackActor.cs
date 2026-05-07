using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umat.Alumni.Member.Api.Services.Interfaces;

namespace Umat.Alumni.Member.Api.Actors;

public class PaystackCallbackActor : ReceiveActor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaystackCallbackActor> _logger;
    private readonly ILoggingAdapter _log;

    public PaystackCallbackActor(IServiceScopeFactory scopeFactory, ILogger<PaystackCallbackActor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _log = Context.GetLogger();

        ReceiveAsync<ProcessPaystackCallbackCommand>(async msg =>
        {
            try
            {
                _log.Info("Processing Paystack callback for reference {0}", msg.Reference);

                using var scope = _scopeFactory.CreateScope();
                var contributionService = scope.ServiceProvider.GetRequiredService<IContributionService>();
                await contributionService.ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Paystack callback for reference {Reference}", msg.Reference);
                _log.Error(ex, "Error processing Paystack callback for reference {Reference}", msg.Reference);
            }
        });
    }
}
