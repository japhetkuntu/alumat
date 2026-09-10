namespace ReservEase.Alumni.Common.Sdk.Services;

/// <summary>
/// A thread-safe, periodically-refreshed set of every institution's
/// Institution.CustomDomain — lets CorsExtensions.IsAllowedOrigin recognize a
/// custom-domain institution's frontend without Common.Sdk needing a
/// database dependency (which would create a circular project reference,
/// since PostgresDb.Sdk already depends on Common.Sdk). Whoever populates
/// this (see each API's own background refresher, which does have DB
/// access) owns the refresh cadence and staleness trade-off; this class is
/// just the shared, lock-free read/write surface.
/// </summary>
public interface ICustomDomainCache
{
    bool Contains(string host);
    void Set(IEnumerable<string> domains);
}

public class CustomDomainCache : ICustomDomainCache
{
    private volatile HashSet<string> domains = new(StringComparer.OrdinalIgnoreCase);

    public bool Contains(string host) => domains.Contains(host);

    /// <summary>Swaps in a fresh snapshot atomically — readers never see a partially-rebuilt set.</summary>
    public void Set(IEnumerable<string> newDomains) =>
        domains = new HashSet<string>(newDomains, StringComparer.OrdinalIgnoreCase);
}
