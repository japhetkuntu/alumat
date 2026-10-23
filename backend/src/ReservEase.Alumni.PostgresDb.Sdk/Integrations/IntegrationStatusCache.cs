using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Sdk.Integrations;

/// <summary>
/// Remembers that a student ID already belongs to a member, so a partner that checks the same ID again and again does not reach
/// the database. Only "exists" is stored: "not found" changes the moment the student registers, and each one issues a link.
/// It sits in the Redis database both the Institution and Member APIs share, so a member being deleted in either one can drop the
/// entry. Redis being down never fails a check; it just means the database answers.
/// </summary>
public class IntegrationStatusCache(IRedisService<PublicContentCacheConfig> cache)
{
    private sealed record Marker(bool Exists);

    private static string Key(string institutionId, string studentId) => $"integration:exists:{institutionId}:{studentId.Trim().ToLowerInvariant()}";

    public async Task<bool> KnownToExistAsync(string institutionId, string studentId)
    {
        try { return (await cache.GetAsync<Marker>(Key(institutionId, studentId)))?.Exists == true; }
        catch { return false; }
    }

    public async Task RememberAsync(string institutionId, string studentId, int days)
    {
        try { await cache.SetAsync(Key(institutionId, studentId), new Marker(true), TimeSpan.FromDays(Math.Max(1, days))); }
        catch { /* the next check simply asks the database again */ }
    }

    /// <summary>Drops the entry, for when a member with that ID is deleted or has their ID changed.</summary>
    public async Task ForgetAsync(string institutionId, string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId)) return;
        try { await cache.RemoveAsync(Key(institutionId, studentId)); }
        catch { /* the entry expires on its own */ }
    }
}
