using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Member.Api.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Actors;

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

                // StoreOrder rows are created synchronously at checkout initiation
                // (unlike Contribution, which is only created once the webhook
                // confirms) — so a cheap existence check reliably tells us which
                // flow owns this reference, with no shared metadata lookup needed.
                var storeOrderService = scope.ServiceProvider.GetRequiredService<IStoreOrderService>();
                if (await storeOrderService.OwnsReferenceAsync(msg.Reference))
                {
                    await storeOrderService.ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
                    return;
                }

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
