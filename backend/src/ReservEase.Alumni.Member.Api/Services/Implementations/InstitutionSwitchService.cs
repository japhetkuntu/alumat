using System.Security.Cryptography;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.Redis.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>Another community this person belongs to, and the portal address to open it at.</summary>
public sealed record SwitchTarget(string InstitutionId, string Slug, string Name, string? LogoUrl, string PortalUrl);

/// <summary>
/// Lets a signed-in member move to the other communities they belong to without signing in again. Member accounts are
/// separate per community, so "the same person" means the same email on an Active, email-verified account in each.
///
/// Why this is safe: an account's password is only ever set by its owner proving the mailbox (the registration one-time code, a
/// Google sign-in, or the reset link emailed to them; imported members start with an unusable password). So a successful
/// sign-in to one account is proof of that mailbox, and the same mailbox on the other account is the same person. The hand-over
/// is one short-lived, single-use code bound to the exact target account and to the portal it is redeemed on.
/// </summary>
public interface IInstitutionSwitchService
{
    Task<IReadOnlyList<SwitchTarget>> ListAsync(string memberId);

    /// <summary>The address to send the browser to, with the one-time code on it, or null when that community is not one of theirs.</summary>
    Task<string?> StartAsync(string memberId, string targetInstitutionId);

    /// <summary>Redeems a code on the portal it was made for. Null for an unknown, used, expired or misdirected code.</summary>
    Task<MemberEntity?> ConsumeAsync(string code);
}

public class InstitutionSwitchService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IRedisService<MemberRedisConfig> redis,
    ICurrentTenantService tenant,
    IConfiguration configuration,
    ILogger<InstitutionSwitchService> logger) : IInstitutionSwitchService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(60);

    private sealed class Handover
    {
        public string TargetMemberId { get; set; } = "";
        public string TargetInstitutionId { get; set; } = "";
    }

    private static string Key(string code) => $"member:switch:{code}";

    public async Task<IReadOnlyList<SwitchTarget>> ListAsync(string memberId)
    {
        var me = await memberRepo.GetByIdAsync(memberId);
        if (me is null || me.Status != "Active" || !me.IsEmailVerified || string.IsNullOrWhiteSpace(me.Email)) return [];

        var email = me.Email.Trim().ToLowerInvariant();
        var others = (await memberRepo.GetAllAsync(
            m => m.Email.ToLower() == email && m.Id != me.Id && m.Status == "Active" && m.IsEmailVerified,
            ignoreQueryFilters: true)).ToList();
        if (others.Count == 0) return [];

        var ids = others.Select(m => m.InstitutionId).Where(i => i != me.InstitutionId).Distinct().ToList();
        var institutions = (await institutionRepo.GetAllAsync(i => ids.Contains(i.Id) && i.Status == "Active", ignoreQueryFilters: true)).ToList();
        var baseDomain = configuration["MemberPortalBaseDomain"] ?? configuration["PlatformBaseDomain"];

        return institutions
            .Select(i => new SwitchTarget(i.Id, i.Slug, string.IsNullOrWhiteSpace(i.PortalName) ? i.Name : i.PortalName!, i.LogoUrl,
                MemberPortalLinks.UrlOrEmpty(i.Slug, i.CustomDomain, baseDomain)))
            .Where(t => !string.IsNullOrEmpty(t.PortalUrl))
            .OrderBy(t => t.Name)
            .ToList();
    }

    public async Task<string?> StartAsync(string memberId, string targetInstitutionId)
    {
        var targets = await ListAsync(memberId);
        var target = targets.FirstOrDefault(t => t.InstitutionId == targetInstitutionId);
        if (target is null) return null;

        var me = await memberRepo.GetByIdAsync(memberId);
        var email = me!.Email.Trim().ToLowerInvariant();
        var theirs = (await memberRepo.GetAllAsync(
            m => m.Email.ToLower() == email && m.InstitutionId == targetInstitutionId && m.Status == "Active" && m.IsEmailVerified,
            ignoreQueryFilters: true)).FirstOrDefault();
        if (theirs is null) return null;

        var code = Base64Url(RandomNumberGenerator.GetBytes(24));
        await redis.SetAsync(Key(code), new Handover { TargetMemberId = theirs.Id, TargetInstitutionId = targetInstitutionId }, CodeLifetime);
        logger.LogInformation("Member {MemberId} started a switch to institution {InstitutionId}", memberId, targetInstitutionId);
        return $"{target.PortalUrl.TrimEnd('/')}/switch?code={code}";
    }

    public async Task<MemberEntity?> ConsumeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length is < 20 or > 80) return null;

        var handover = await redis.GetAsync<Handover>(Key(code));
        if (handover is null) return null;
        await redis.RemoveAsync(Key(code));   // single use, whatever happens next

        // Only valid on the portal it was made for: a code carried to another community's host does nothing.
        if (tenant.InstitutionId != handover.TargetInstitutionId) return null;

        var member = await memberRepo.GetByIdAsync(handover.TargetMemberId);
        return member is { Status: "Active" } ? member : null;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
