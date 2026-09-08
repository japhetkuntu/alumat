using ReservEase.Alumni.Redis.Sdk.Models;

namespace ReservEase.Alumni.Common.Sdk.Options;

/// <summary>
/// Shared Redis database for the public (unauthenticated) landing-page content
/// cache — news/events/spotlights/theme. Both Member.Api (reads, and writes
/// on a cache miss) and Institution.Api (invalidates on admin edits) connect
/// to this same logical database, on the one Redis server every app already
/// shares (see MemberRedisConfig/InstitutionRedisConfig — same ConnectionString,
/// different DbNumber). A distinct DbNumber from every app-specific database
/// keeps this cache's keys out of their way.
/// </summary>
public class PublicContentCacheConfig : IRedisDatabaseConfig
{
    public string Alias { get; set; } = "PublicContentCache";
    public int DbNumber { get; set; } = 3;
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan? DefaultExpiry => TimeSpan.FromMinutes(10);
}
