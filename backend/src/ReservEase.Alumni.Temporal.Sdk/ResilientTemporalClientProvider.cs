using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace ReservEase.Alumni.Temporal.Sdk;

/// <summary>
/// Connects to Temporal once at construction. A connection failure is logged at
/// Critical level and swallowed so the application keeps starting and serving
/// requests. In that degraded state <see cref="IsAvailable"/> is <c>false</c> and
/// <see cref="Client"/> is <c>null</c>; callers skip orchestration rather than
/// failing the request outright.
/// </summary>
public class ResilientTemporalClientProvider : ITemporalClientProvider
{
    public ITemporalClient? Client { get; }
    public bool IsAvailable { get; }

    public ResilientTemporalClientProvider(
        IOptions<TemporalConfig> config,
        ILogger<ResilientTemporalClientProvider> logger)
    {
        var address = config.Value.Address;
        var @namespace = config.Value.Namespace;
        var apiKey = config.Value.ApiKey;

        try
        {
            Client = TemporalClient.ConnectAsync(new TemporalClientConnectOptions(address)
            {
                Namespace = @namespace,
                ApiKey = apiKey,
            }).GetAwaiter().GetResult();

            IsAvailable = true;
            logger.LogInformation(
                "Temporal client connected successfully to {Address} (namespace {Namespace}).",
                address, @namespace);
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            Client = null;
            logger.LogCritical(ex,
                "Failed to connect to Temporal server at {Address} (namespace {Namespace}). " +
                "Proceeding without Temporal orchestration; workflow-backed requests will fail " +
                "fast rather than orchestrate.",
                address, @namespace);
        }
    }
}
