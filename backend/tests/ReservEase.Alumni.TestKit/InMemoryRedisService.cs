using ReservEase.Alumni.Redis.Sdk.Models;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.TestKit;

/// <summary>A dictionary-backed <see cref="IRedisService{TConfig}"/> that round-trips values through JSON like the real one.</summary>
public sealed class InMemoryRedisService<TConfig> : IRedisService<TConfig> where TConfig : IRedisDatabaseConfig
{
    private readonly Dictionary<string, string> store = new();
    public List<string> Reads { get; } = new();
    public List<string> Writes { get; } = new();
    public IReadOnlyDictionary<string, string> Store => store;

    public Task<T?> GetAsync<T>(string key)
    {
        Reads.Add(key);
        return Task.FromResult(store.TryGetValue(key, out var json) ? Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json) : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        Writes.Add(key);
        store[key] = Newtonsoft.Json.JsonConvert.SerializeObject(value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key) { store.Remove(key); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string key) => Task.FromResult(store.ContainsKey(key));
}
