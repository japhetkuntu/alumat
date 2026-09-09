using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Periodically walks every institution and sends any re-engagement digest
/// emails that are due. There's no prior scheduled-job infrastructure in this
/// codebase, so this is a plain BackgroundService: a fresh DI scope per
/// institution (mirrors NotificationDispatcherActor's per-command scope),
/// with ICurrentTenantService set manually since no HttpContext ever runs
/// through this loop for TenantResolutionMiddleware to populate it.
///
/// A 6-hour tick is frequent enough that "weekly"/"monthly" due-checks (done
/// in IDigestService against each member's LastDigestSentAt) stay accurate
/// to within a few hours, without hammering the DB on every restart.
/// </summary>
public class DigestSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<DigestSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

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
            // The institution lookup below (and anything else outside the
            // per-institution try/catch) must never throw out of here.
            // HostOptions.BackgroundServiceExceptionBehavior is StopHost
            // (the app-wide default), so ANY unhandled exception from a
            // BackgroundService takes down the entire process, not just this
            // scheduler.
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Digest scheduler cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        List<string> institutionIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();
            institutionIds = await db.Institutions.IgnoreQueryFilters()
                .Where(i => i.Status == "Active")
                .Select(i => i.Id)
                .ToListAsync(stoppingToken);
        }

        logger.LogInformation("Digest scheduler cycle starting for {Count} institutions", institutionIds.Count);

        foreach (var institutionId in institutionIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ICurrentTenantService>().SetInstitutionId(institutionId);
                var digestService = scope.ServiceProvider.GetRequiredService<IDigestService>();
                await digestService.SendDueDigestsAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Digest cycle failed for institution {InstitutionId}", institutionId);
            }
        }
    }
}
