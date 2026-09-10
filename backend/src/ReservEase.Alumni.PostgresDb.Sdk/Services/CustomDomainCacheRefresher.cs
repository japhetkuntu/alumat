using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Keeps ICustomDomainCache (used by CorsExtensions.IsAllowedOrigin) in sync
/// with every institution's Institution.CustomDomain, since a custom domain
/// can be added at any time via Platform.Api. Refreshes once at startup and
/// every 5 minutes after — new/changed custom domains take up to that long
/// to start being accepted by CORS, which is an acceptable trade-off for
/// something that requires separate DNS/TLS provisioning anyway.
/// </summary>
public class CustomDomainCacheRefresher(
    IServiceScopeFactory scopeFactory, ICustomDomainCache cache, ILogger<CustomDomainCacheRefresher> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();
            var domains = await db.Institutions.IgnoreQueryFilters()
                .Where(i => i.CustomDomain != null)
                .Select(i => i.CustomDomain!)
                .ToListAsync(cancellationToken);
            cache.Set(domains);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Never let a refresh failure take the cache to empty — keep serving
            // whatever was last successfully loaded until the next tick recovers.
            logger.LogError(e, "Failed to refresh custom-domain CORS cache");
        }
    }
}
