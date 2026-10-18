using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// The tenant lookup (see TenantResolutionMiddleware) keeps the whole institution row in Redis for a minute, and that row is what the
/// theme, the feature switches and the wording come from. Left alone, a saved change (branding, features, wording, a switch the platform
/// flips) would not show for up to a minute and a refresh would not help. So any save that changes an institution removes its cached
/// lookups straight afterwards. Only APIs that run the cache are affected; one that does not (the platform API) simply has no cache to clear,
/// and its changes show within the cache's own minute.
/// </summary>
public class TenantCacheInvalidationInterceptor(IServiceProvider services, IConfiguration config, ILogger<TenantCacheInvalidationInterceptor> logger) : SaveChangesInterceptor
{
    private readonly HashSet<string> _keys = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await FlushAsync();
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAsync().GetAwaiter().GetResult();
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _keys.Clear();

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _keys.Clear();
        return Task.CompletedTask;
    }

    private void Collect(DbContext? db)
    {
        if (db is null) return;
        var baseDomain = config["PlatformBaseDomain"];
        foreach (var entry in db.ChangeTracker.Entries<Institution>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            // Both the old and the new address, in case the slug or custom domain is the thing that changed.
            foreach (var slug in new[] { entry.OriginalValues.GetValue<string>(nameof(Institution.Slug)), entry.Entity.Slug }.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                _keys.Add($"slug:{slug}");
                if (!string.IsNullOrWhiteSpace(baseDomain)) _keys.Add($"host:{slug}.{baseDomain}".ToLowerInvariant());
            }
            foreach (var domain in new[] { entry.OriginalValues.GetValue<string?>(nameof(Institution.CustomDomain)), entry.Entity.CustomDomain }.Where(d => !string.IsNullOrWhiteSpace(d)))
                _keys.Add($"host:{domain}".ToLowerInvariant());
        }
    }

    private async Task FlushAsync()
    {
        if (_keys.Count == 0) return;
        var keys = _keys.ToList();
        _keys.Clear();
        try
        {
            var cache = services.GetService<IRedisService<TenantResolutionCacheConfig>>();
            if (cache is null) return;
            foreach (var key in keys) await cache.RemoveAsync($"tenant-resolution:{key}");
        }
        catch (Exception e)
        {
            // The change is already saved; the cache clears itself within its minute.
            logger.LogWarning(e, "Could not clear the cached tenant lookup after an institution changed");
        }
    }
}
