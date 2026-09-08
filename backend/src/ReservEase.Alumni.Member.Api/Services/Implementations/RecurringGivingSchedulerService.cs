using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Daily cross-tenant sweep that charges every RecurringContribution due
/// today — same cross-tenant BackgroundService shape as DigestSchedulerService
/// (fresh DI scope per institution, ICurrentTenantService set manually since
/// no HttpContext runs through this loop). A daily tick (rather than
/// DigestSchedulerService's 6-hour one) is what the 3-day dunning retry on a
/// failed charge actually needs; a monthly cadence doesn't need finer than that.
/// </summary>
public class RecurringGivingSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringGivingSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(3);

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
            await RunCycleAsync(stoppingToken);
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
                .Where(i => i.Status == "Active" || i.Status == "Trial")
                .Select(i => i.Id)
                .ToListAsync(stoppingToken);
        }

        logger.LogInformation("Recurring giving scheduler cycle starting for {Count} institutions", institutionIds.Count);

        foreach (var institutionId in institutionIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ICurrentTenantService>().SetInstitutionId(institutionId);
                var processor = scope.ServiceProvider.GetRequiredService<IRecurringGivingProcessor>();
                await processor.ChargeDueRecurringGivingAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Recurring giving cycle failed for institution {InstitutionId}", institutionId);
            }
        }
    }
}
