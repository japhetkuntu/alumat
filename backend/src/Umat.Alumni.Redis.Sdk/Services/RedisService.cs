using Newtonsoft.Json;
using StackExchange.Redis;
using Umat.Alumni.Redis.Sdk.Models;

namespace Umat.Alumni.Redis.Sdk.Services;

public class RedisService<TConfig>(TConfig config) : IRedisService<TConfig>
    where TConfig : IRedisDatabaseConfig
{
    private readonly IDatabase _db = ConnectionMultiplexer.Connect(config.ConnectionString).GetDatabase(config.DbNumber);
    private readonly TimeSpan? _defaultExpiry = config.DefaultExpiry;

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.IsNullOrEmpty ? default : JsonConvert.DeserializeObject<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonConvert.SerializeObject(value);
        await _db.StringSetAsync(key, json, expiry ?? _defaultExpiry);
    }

    public async Task RemoveAsync(string key)
        => await _db.KeyDeleteAsync(key);

    public async Task<bool> ExistsAsync(string key)
        => await _db.KeyExistsAsync(key);
}
