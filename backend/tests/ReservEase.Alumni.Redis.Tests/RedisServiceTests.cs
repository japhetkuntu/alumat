using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Redis.Sdk.Extensions;
using ReservEase.Alumni.Redis.Sdk.Models;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Redis.Tests;

public class TestRedisConfig : IRedisDatabaseConfig
{
    public string Alias { get; set; } = "test";
    public int DbNumber { get; set; }
    public string ConnectionString { get; set; } = "";
    public TimeSpan? DefaultExpiry { get; set; }
}

/// <summary>
/// Redis is a cache here, so every failure must degrade to "miss" rather than throw. These tests
/// run against a connection string that can never connect, so they need no real Redis server.
/// </summary>
public class RedisServiceTests
{
    private static RedisService<TestRedisConfig> Create(string connectionString) =>
        new(new TestRedisConfig { ConnectionString = connectionString }, NullLogger<RedisService<TestRedisConfig>>.Instance);

    public static IEnumerable<object[]> Unavailable() => new[]
    {
        new object[] { "127.0.0.1:1,connectTimeout=100" },   // refused
        new object[] { "this is :: not a connection string" }, // unparseable
        new object[] { "" },
    };

    [Theory, MemberData(nameof(Unavailable))]
    public async Task Get_returns_default_when_redis_is_unavailable(string cs)
        => Assert.Null(await Create(cs).GetAsync<string>("k"));

    [Theory, MemberData(nameof(Unavailable))]
    public async Task Set_does_not_throw_when_redis_is_unavailable(string cs)
        => await Create(cs).SetAsync("k", new { A = 1 }, TimeSpan.FromMinutes(1));

    [Theory, MemberData(nameof(Unavailable))]
    public async Task Remove_does_not_throw_when_redis_is_unavailable(string cs)
        => await Create(cs).RemoveAsync("k");

    [Theory, MemberData(nameof(Unavailable))]
    public async Task Exists_is_false_when_redis_is_unavailable(string cs)
        => Assert.False(await Create(cs).ExistsAsync("k"));

    [Fact]
    public async Task Repeated_calls_after_a_failure_stay_fast_thanks_to_the_breaker()
    {
        var service = Create("127.0.0.1:1,connectTimeout=100");
        await service.GetAsync<string>("k");

        var started = DateTime.UtcNow;
        for (var i = 0; i < 50; i++) await service.GetAsync<string>("k");

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddRedisDatabase_binds_the_section_named_after_the_config_type_and_registers_service()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Redis:TestRedisConfig:Alias"] = "cache",
            ["Redis:TestRedisConfig:DbNumber"] = "4",
            ["Redis:TestRedisConfig:ConnectionString"] = "127.0.0.1:1",
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddRedisDatabase<TestRedisConfig>(configuration);
        using var provider = services.BuildServiceProvider();

        var bound = provider.GetRequiredService<TestRedisConfig>();
        Assert.Equal("cache", bound.Alias);
        Assert.Equal(4, bound.DbNumber);
        Assert.IsType<RedisService<TestRedisConfig>>(provider.GetRequiredService<IRedisService<TestRedisConfig>>());
        Assert.Same(provider.GetRequiredService<IRedisService<TestRedisConfig>>(), provider.GetRequiredService<IRedisService<TestRedisConfig>>());
    }
}
