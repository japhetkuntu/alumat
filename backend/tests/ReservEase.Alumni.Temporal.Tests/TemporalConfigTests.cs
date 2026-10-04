using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Temporal.Sdk;

namespace ReservEase.Alumni.Temporal.Tests;

public class TemporalConfigTests
{
    [Fact]
    public void Defaults_target_a_local_dev_server()
    {
        var c = new TemporalConfig();
        Assert.Equal("localhost:7233", c.Address);
        Assert.Equal("default", c.Namespace);
        Assert.Null(c.ApiKey);
    }

    [Fact]
    public void AddTemporalClientProvider_binds_config_and_registers_a_singleton_provider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TemporalConfig:Address"] = "temporal.internal:7233",
            ["TemporalConfig:Namespace"] = "prod",
            ["TemporalConfig:ApiKey"] = "key",
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddTemporalClientProvider(configuration);

        using var provider = services.BuildServiceProvider();
        var bound = provider.GetRequiredService<IOptions<TemporalConfig>>().Value;
        Assert.Equal("temporal.internal:7233", bound.Address);
        Assert.Equal("prod", bound.Namespace);
        Assert.Equal("key", bound.ApiKey);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ITemporalClientProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(ResilientTemporalClientProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void Provider_degrades_to_unavailable_when_the_server_cannot_be_reached()
    {
        var provider = new ResilientTemporalClientProvider(
            Options.Create(new TemporalConfig { Address = "127.0.0.1:1" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResilientTemporalClientProvider>.Instance);

        Assert.False(provider.IsAvailable);
        Assert.Null(provider.Client);
    }
}
