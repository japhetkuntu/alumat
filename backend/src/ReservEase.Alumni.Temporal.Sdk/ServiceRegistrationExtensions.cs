using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ReservEase.Alumni.Temporal.Sdk;

public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Registers a Temporal client for a process that only starts workflows (never runs a
    /// worker) — matches the AddPaystackService/AddMailtrapEmailService convention of one
    /// POCO + section per integration. The client connects once at startup and degrades
    /// gracefully (see <see cref="ResilientTemporalClientProvider"/>) rather than crashing
    /// the API if Temporal is unreachable.
    /// </summary>
    public static IServiceCollection AddTemporalClientProvider(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TemporalConfig>(configuration.GetSection(nameof(TemporalConfig)));
        services.AddSingleton<ITemporalClientProvider, ResilientTemporalClientProvider>();
        return services;
    }
}
