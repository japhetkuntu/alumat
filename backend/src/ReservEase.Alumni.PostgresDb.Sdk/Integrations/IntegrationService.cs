using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.PostgresDb.Sdk.Integrations;

/// <summary>Who a valid API key belongs to, and the institution's integration settings, found in one read.</summary>
public record IntegrationCaller(
    string KeyId, DateTime? KeyLastUsedAt, string InstitutionId, string InstitutionSlug, string? CustomDomain, bool InstitutionSuspended,
    bool Enabled, int LinkExpiryHours, int StatusCacheDays);

/// <summary>What the member portal shows at the top of every page for a while after someone registers through a partner link.</summary>
public record IntegrationBanner(string PortalName, string RedirectUrl, string Message, DateTime ExpiresAt);

public enum LinkState { Valid, NotFound, Expired, Used, Replaced }

public record LinkLookup(LinkState State, OnboardingLink? Link, InstitutionIntegration? Settings);

/// <summary>
/// The partner-onboarding rules shared by the APIs that touch them: finding the caller behind an API key, asking whether a student
/// ID is already a member, and issuing, checking and spending registration links.
/// </summary>
public class IntegrationService(
    IAlumniPgRepository<IntegrationApiKey> keyRepo,
    IAlumniPgRepository<InstitutionIntegration> settingsRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<OnboardingLink> linkRepo,
    IAlumniPgRepository<Member> memberRepo)
{
    /// <summary>How often a key's "last used" is written, so a busy partner does not turn every call into a write.</summary>
    private static readonly TimeSpan LastUsedEvery = TimeSpan.FromMinutes(5);

    /// <summary>The caller behind a raw API key, or null when the key is unknown or revoked. One indexed read.</summary>
    public async Task<IntegrationCaller?> AuthenticateAsync(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || rawKey.Length > 200) return null;
        var hash = IntegrationSecrets.Hash(rawKey.Trim());

        var caller = await (
            from k in keyRepo.GetQueryable(k => k.KeyHash == hash && k.RevokedAt == null, ignoreQueryFilters: true)
            join s in settingsRepo.GetQueryable(null, ignoreQueryFilters: true) on k.InstitutionId equals s.InstitutionId
            join i in institutionRepo.GetQueryable(null, ignoreQueryFilters: true) on k.InstitutionId equals i.Id
            select new IntegrationCaller(k.Id, k.LastUsedAt, i.Id, i.Slug, i.CustomDomain, i.Status == "Suspended", s.Enabled, s.LinkExpiryHours, s.StatusCacheDays))
            .FirstOrDefaultAsync();

        if (caller is not null && (caller.KeyLastUsedAt is null || DateTime.UtcNow - caller.KeyLastUsedAt > LastUsedEvery))
        {
            var now = DateTime.UtcNow;
            await keyRepo.ExecuteUpdateAsync(k => k.Id == caller.KeyId, s => s.SetProperty(k => k.LastUsedAt, now), ignoreQueryFilters: true);
        }
        return caller;
    }

    /// <summary>Whether this institution has any member with that student ID, in any status, ignoring case.</summary>
    public Task<bool> StudentExistsAsync(string institutionId, string studentId)
    {
        var id = studentId.ToLower();
        return memberRepo.GetQueryable(m => m.InstitutionId == institutionId && m.StudentId != null && m.StudentId.ToLower() == id, ignoreQueryFilters: true).AnyAsync();
    }

    /// <summary>Issues a link for a student ID, replacing any earlier unused one for the same ID. Returns the raw token (shown once) and when it expires.</summary>
    public async Task<(string Token, DateTime ExpiresAt)> IssueLinkAsync(string institutionId, string studentId, int expiryHours)
    {
        var now = DateTime.UtcNow;
        var sid = studentId.ToLower();
        await linkRepo.ExecuteUpdateAsync(
            l => l.InstitutionId == institutionId && l.StudentId.ToLower() == sid && l.ConsumedAt == null && l.SupersededAt == null,
            s => s.SetProperty(l => l.SupersededAt, now), ignoreQueryFilters: true);

        var (token, hash) = IntegrationSecrets.NewLinkToken();
        var expires = now.AddHours(Math.Max(1, expiryHours));
        await linkRepo.AddAsync(new OnboardingLink { InstitutionId = institutionId, StudentId = studentId, TokenHash = hash, ExpiresAt = expires, CreatedBy = "integration" });
        return (token, expires);
    }

    /// <summary>What state a link is in, with its settings. Scoped to the institution of the current request.</summary>
    public async Task<LinkLookup> LookupLinkAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return new LinkLookup(LinkState.NotFound, null, null);
        var hash = IntegrationSecrets.Hash(token.Trim());
        var link = await linkRepo.GetOneAsync(l => l.TokenHash == hash);
        if (link is null) return new LinkLookup(LinkState.NotFound, null, null);

        var settings = await settingsRepo.GetOneAsync(s => s.InstitutionId == link.InstitutionId, ignoreQueryFilters: true);
        var state =
            settings is not { Enabled: true } ? LinkState.NotFound
            : link.ConsumedAt is not null ? LinkState.Used
            : link.SupersededAt is not null ? LinkState.Replaced
            : link.ExpiresAt <= DateTime.UtcNow ? LinkState.Expired
            : LinkState.Valid;
        return new LinkLookup(state, link, settings);
    }

    /// <summary>The banner for a member who registered through a partner link, or null once it has run out, or when the integration has been turned off or never filled in.</summary>
    public async Task<IntegrationBanner?> BannerAsync(string institutionId, DateTime? until)
    {
        if (until is null || until <= DateTime.UtcNow) return null;
        var s = await settingsRepo.GetOneAsync(x => x.InstitutionId == institutionId, ignoreQueryFilters: true);
        return s is { Enabled: true } && s.RedirectUrl.Length > 0 && s.BannerMessage.Length > 0
            ? new IntegrationBanner(s.PortalName, s.RedirectUrl, s.BannerMessage, until.Value)
            : null;
    }

    /// <summary>Spends a link for a member. Atomic: of two simultaneous attempts only one gets true.</summary>
    public async Task<bool> TryConsumeLinkAsync(string linkId, string memberId)
    {
        var now = DateTime.UtcNow;
        var changed = await linkRepo.ExecuteUpdateAsync(
            l => l.Id == linkId && l.ConsumedAt == null && l.SupersededAt == null && l.ExpiresAt > now,
            s => s.SetProperty(l => l.ConsumedAt, now).SetProperty(l => l.MemberId, memberId), ignoreQueryFilters: true);
        return changed == 1;
    }
}
