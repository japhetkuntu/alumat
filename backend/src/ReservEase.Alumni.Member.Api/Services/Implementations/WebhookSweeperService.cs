using Akka.Actor;
using Akka.Routing;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Member.Api.Actors;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Re-drives WebhookEvent inbox rows that never made it to ProcessedAt — the case the
/// PaystackCallbackActor's own catch block can't cover (the process dying before that
/// actor ever ran, or the in-memory Tell never arriving). WebhookEvent isn't tenant-scoped
/// (see its own doc comment), so unlike DigestSchedulerService this needs no per-institution
/// ICurrentTenantService setup — it queries and re-dispatches globally.
/// </summary>
public class WebhookSweeperService(
    IServiceScopeFactory scopeFactory,
    IActorRef callbackActor,
    ILogger<WebhookSweeperService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private const int MaxAttempts = 5;
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            // HostOptions.BackgroundServiceExceptionBehavior is StopHost (the app-wide
            // default), so this must never let an exception escape the tick.
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Webhook sweeper cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();

        var stuck = await db.WebhookEvents
            .Where(w => w.Provider == "Paystack" && w.ProcessedAt == null && w.Attempts < MaxAttempts)
            .OrderBy(w => w.ReceivedAt)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (stuck.Count == 0) return;

        logger.LogWarning("Webhook sweeper re-dispatching {Count} unprocessed Paystack events", stuck.Count);

        foreach (var webhookEvent in stuck)
        {
            var command = new ProcessPaystackCallbackCommand(webhookEvent.Reference, webhookEvent.RawBody, webhookEvent.Id);
            callbackActor.Tell(new ConsistentHashableEnvelope(command, webhookEvent.Reference));
        }
    }
}
