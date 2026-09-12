using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using ReservEase.Alumni.Redis.Sdk.Models;

namespace ReservEase.Alumni.Redis.Sdk.Services;

/// <summary>
/// Caching is meant to reduce load on Postgres, never to become a new way for
/// the app to go down — several hot paths (tenant resolution runs on every
/// single request) now depend on this class, so every failure mode here is
/// deliberately absorbed rather than thrown: a bad/unparseable connection
/// string, Redis being unreachable at startup, or a mid-request timeout all
/// degrade to "cache disabled for this call" (falling through to whatever the
/// caller does on a miss — normally a direct DB read) instead of a 500 or a
/// failed app startup. <see cref="ConfigurationOptions.AbortOnConnectFail"/>
/// being off is the other half of this — the multiplexer keeps retrying in
/// the background instead of needing this service to be recreated once Redis
/// comes back.
///
/// A short breaker on top of that is what makes the fallback actually usable
/// rather than just non-crashing: a single request that hits a down Redis
/// still pays one real network timeout (bounded low, see the options below),
/// but every call in the few seconds after that skips straight to the
/// fallback instead of each paying that same timeout again — the difference
/// between one slow request and every request being slow for the length of
/// the outage.
/// </summary>
public class RedisService<TConfig> : IRedisService<TConfig>
    where TConfig : IRedisDatabaseConfig
{
    private static readonly TimeSpan BreakerCooldown = TimeSpan.FromSeconds(5);

    private readonly Lazy<IDatabase?> _db;
    private readonly TimeSpan? _defaultExpiry;
    private readonly ILogger<RedisService<TConfig>> _logger;
    private readonly string _alias;
    private DateTime? _brokenUntilUtc;

    public RedisService(TConfig config, ILogger<RedisService<TConfig>> logger)
    {
        _defaultExpiry = config.DefaultExpiry;
        _logger = logger;
        _alias = config.Alias;

        _db = new Lazy<IDatabase?>(() =>
        {
            try
            {
                var options = ConfigurationOptions.Parse(config.ConnectionString);
                options.AbortOnConnectFail = false;
                // The library's 5000ms default timeout, multiplied by however many
                // retries a down Redis triggers, is the difference between "a
                // cache miss" and "every request hangs for several seconds" — a
                // fast, low timeout is what makes the breaker below trip quickly
                // instead of every request paying the full default wait first.
                options.ConnectTimeout = 300;
                options.SyncTimeout = 300;
                options.AsyncTimeout = 300;
                options.ConnectRetry = 0;
                options.KeepAlive = 30;
                return ConnectionMultiplexer.Connect(options).GetDatabase(config.DbNumber);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Redis unavailable for {Alias} (db {DbNumber}) — caching disabled for this instance, falling back to direct reads.", _alias, config.DbNumber);
                return null;
            }
        });
    }

    /// <summary>Null when the breaker is open (skip Redis entirely) or the multiplexer never connected at all.</summary>
    private IDatabase? GetDbIfHealthy()
    {
        if (_brokenUntilUtc is { } until && DateTime.UtcNow < until) return null;
        return _db.Value;
    }

    private void RecordFailure(Exception e, string op, string key)
    {
        _brokenUntilUtc = DateTime.UtcNow.Add(BreakerCooldown);
        _logger.LogWarning(e, "Redis {Op} failed for {Alias} key {Key} — pausing this cache for {Cooldown}s.", op, _alias, key, BreakerCooldown.TotalSeconds);
    }

    private void RecordSuccess() => _brokenUntilUtc = null;

    public async Task<T?> GetAsync<T>(string key)
    {
        if (GetDbIfHealthy() is not { } db) return default;
        try
        {
            var value = await db.StringGetAsync(key);
            RecordSuccess();
            return value.IsNullOrEmpty ? default : JsonConvert.DeserializeObject<T>(value!);
        }
        catch (Exception e)
        {
            RecordFailure(e, "GET", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        if (GetDbIfHealthy() is not { } db) return;
        try
        {
            var json = JsonConvert.SerializeObject(value);
            await db.StringSetAsync(key, json, expiry ?? _defaultExpiry);
            RecordSuccess();
        }
        catch (Exception e)
        {
            RecordFailure(e, "SET", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        if (GetDbIfHealthy() is not { } db) return;
        try
        {
            await db.KeyDeleteAsync(key);
            RecordSuccess();
        }
        catch (Exception e)
        {
            RecordFailure(e, "DEL", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (GetDbIfHealthy() is not { } db) return false;
        try
        {
            var result = await db.KeyExistsAsync(key);
            RecordSuccess();
            return result;
        }
        catch (Exception e)
        {
            RecordFailure(e, "EXISTS", key);
            return false;
        }
    }
}
