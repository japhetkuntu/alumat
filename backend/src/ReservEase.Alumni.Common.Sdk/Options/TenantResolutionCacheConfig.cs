using ReservEase.Alumni.Redis.Sdk.Models;

namespace ReservEase.Alumni.Common.Sdk.Options;

/// <summary>
/// Shared Redis database for TenantResolutionMiddleware's host-to-Institution
/// lookup — the one DB query that runs on literally every request to
/// Member.Api and Institution.Api (not just public/theme endpoints), so
/// caching it is the single highest-leverage way to cut Postgres load. Both
/// apps bind to this same logical database (same ConnectionString, distinct
/// DbNumber from every other Redis config) the same way PublicContentCacheConfig
/// is shared between them.
///
/// A short TTL, not write-triggered invalidation, is the deliberate choice
/// here: an Institution's Status/CustomDomain/Slug can change from three
/// different services (the institution's own settings, and two places in
/// Platform.Api), and this middleware is far too hot a path to make every one
/// of those writes remember to poke a cache. A 60s bound is an acceptable
/// trade-off — it still cuts DB hits by roughly two orders of magnitude on
/// any host with real traffic, while keeping "an institution got suspended"
/// or "a custom domain changed" a same-minute effect rather than a stale one.
/// </summary>
public class TenantResolutionCacheConfig : IRedisDatabaseConfig
{
    public string Alias { get; set; } = "TenantResolutionCache";
    public int DbNumber { get; set; } = 4;
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan? DefaultExpiry => TimeSpan.FromSeconds(60);
}
