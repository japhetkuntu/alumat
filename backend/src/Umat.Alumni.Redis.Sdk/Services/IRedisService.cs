using Umat.Alumni.Redis.Sdk.Models;

namespace Umat.Alumni.Redis.Sdk.Services;

public interface IRedisService<TConfig> where TConfig : IRedisDatabaseConfig
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
}
