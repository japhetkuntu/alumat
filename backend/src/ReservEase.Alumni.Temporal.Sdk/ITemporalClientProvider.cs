using Temporalio.Client;

namespace ReservEase.Alumni.Temporal.Sdk;

/// <summary>
/// Provides access to the Temporal client with graceful degradation.
/// When the Temporal server is unreachable, <see cref="IsAvailable"/> is
/// <c>false</c> and <see cref="Client"/> is <c>null</c>. Callers MUST check
/// <see cref="IsAvailable"/> before using <see cref="Client"/> so requests can
/// continue to be processed without workflow orchestration.
/// </summary>
public interface ITemporalClientProvider
{
    /// <summary>
    /// The connected Temporal client, or <c>null</c> when the server could not
    /// be reached at startup.
    /// </summary>
    ITemporalClient? Client { get; }

    /// <summary>
    /// <c>true</c> when a Temporal connection was established successfully
    /// <b>at startup</b>. A connectivity loss <i>after</i> startup is detected
    /// from the workflow call itself rather than this flag.
    /// </summary>
    bool IsAvailable { get; }
}
