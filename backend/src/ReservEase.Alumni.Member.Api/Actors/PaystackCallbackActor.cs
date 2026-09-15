using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Actors;

/// <summary>
/// One instance runs behind a consistent-hash pool router (see AddActorSystem), keyed by
/// Reference — so callbacks for the same payment reference are always handled by the same
/// routee and stay strictly ordered, while callbacks for different references can run on
/// different routees in parallel instead of queueing behind one shared mailbox.
/// </summary>
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
            using var scope = _scopeFactory.CreateScope();
            var webhookEventRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<WebhookEvent>>();

            try
            {
                _log.Info("Processing Paystack callback for reference {0}", msg.Reference);

                var claimed = await RouteToOwnerAsync(scope.ServiceProvider, msg);
                if (!claimed)
                    _log.Warning("Paystack callback for reference {0} was not claimed by any service", msg.Reference);

                var webhookEvent = await webhookEventRepo.GetByIdAsync(msg.WebhookEventId);
                if (webhookEvent is not null)
                {
                    webhookEvent.ProcessedAt = DateTime.UtcNow;
                    webhookEvent.Attempts += 1;
                    await webhookEventRepo.UpdateAsync(webhookEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Paystack callback for reference {Reference}", msg.Reference);
                _log.Error(ex, "Error processing Paystack callback for reference {0}", msg.Reference);

                // Leave ProcessedAt unset so the event stays visible as unprocessed for a
                // replay/alerting sweep — only record the attempt and the failure reason.
                var webhookEvent = await webhookEventRepo.GetByIdAsync(msg.WebhookEventId);
                if (webhookEvent is not null)
                {
                    webhookEvent.Attempts += 1;
                    webhookEvent.LastError = ex.Message;
                    await webhookEventRepo.UpdateAsync(webhookEvent);
                }
            }
        });
    }

    /// <summary>Returns true once some service has claimed and processed the reference.</summary>
    private static async Task<bool> RouteToOwnerAsync(IServiceProvider services, ProcessPaystackCallbackCommand msg)
    {
        // References minted after the prefix scheme shipped carry SO_/SR_/CN_ up front,
        // so which service owns the callback is known from the string alone — no
        // existence-check DB round trips needed, and no chance of guessing wrong.
        var prefix = PaystackReferencePrefix.ExtractPrefix(msg.Reference);
        switch (prefix)
        {
            case PaystackReferencePrefix.StoreOrder:
                await services.GetRequiredService<IStoreOrderService>().ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
                return true;

            case PaystackReferencePrefix.ServiceRequest:
                await services.GetRequiredService<IServiceRequestService>().ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
                return true;

            case PaystackReferencePrefix.Contribution:
                await services.GetRequiredService<IContributionService>().ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
                return true;
        }

        // Unprefixed reference — it was initiated before this scheme shipped and is still
        // in flight. Fall back to the old guess-by-existence-check chain so it still gets
        // processed; every reference minted from here on will always hit the switch above.
        return await RouteByExistenceCheckAsync(services, msg);
    }

    private static async Task<bool> RouteByExistenceCheckAsync(IServiceProvider services, ProcessPaystackCallbackCommand msg)
    {
        var storeOrderService = services.GetRequiredService<IStoreOrderService>();
        if (await storeOrderService.OwnsReferenceAsync(msg.Reference))
        {
            await storeOrderService.ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
            return true;
        }

        var serviceRequestService = services.GetRequiredService<IServiceRequestService>();
        if (await serviceRequestService.OwnsReferenceAsync(msg.Reference))
        {
            await serviceRequestService.ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
            return true;
        }

        var contributionService = services.GetRequiredService<IContributionService>();
        if (await contributionService.OwnsReferenceAsync(msg.Reference))
        {
            await contributionService.ProcessPaystackCallbackAsync(msg.Reference, msg.RawBody);
            return true;
        }

        return false;
    }
}
