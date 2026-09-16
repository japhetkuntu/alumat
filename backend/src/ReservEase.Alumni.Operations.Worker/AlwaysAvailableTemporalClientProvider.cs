using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Client;

namespace ReservEase.Alumni.Operations.Worker;

/// <summary>
/// Adapts this process's own lazy ITemporalClient (registered by AddTemporalClient for the
/// hosted workers) into ITemporalClientProvider, so activities that call
/// EnqueueNotificationAsync (e.g. ContributionCallbackActivities, ScheduledJobsActivities)
/// can be constructed here too. Unlike ResilientTemporalClientProvider — built for a web API
/// that must keep serving requests even if Temporal is unreachable — this worker process's
/// entire job IS talking to Temporal, so there's no separate "degraded" client to manage:
/// IsAvailable is always true, and a connectivity problem surfaces as an exception from the
/// call itself (which EnqueueNotificationAsync already catches and logs) rather than a
/// pre-flight flag.
/// </summary>
public class AlwaysAvailableTemporalClientProvider(ITemporalClient client) : ITemporalClientProvider
{
    public ITemporalClient? Client { get; } = client;
    public bool IsAvailable => true;
}
