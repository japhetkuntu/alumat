using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Redis.Sdk.Models;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Redis.Sdk.Extensions;

public static class RedisExtensionService
{
    public static IServiceCollection AddRedisDatabase<TConfig>(
        this IServiceCollection services, IConfiguration configuration)
        where TConfig : class, IRedisDatabaseConfig, new()
    {
        var config = new TConfig();
        configuration.GetSection($"Redis:{typeof(TConfig).Name}").Bind(config);
        services.AddSingleton(config);
        services.AddSingleton<IRedisService<TConfig>, RedisService<TConfig>>(
            sp => new RedisService<TConfig>(sp.GetRequiredService<TConfig>()));

        return services;
    }
}
