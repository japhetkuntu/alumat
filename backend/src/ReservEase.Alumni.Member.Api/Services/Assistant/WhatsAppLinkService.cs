using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>
/// Proves that a signed-in AlumUnion member controls a WhatsApp number, and records it on the member's own row.
///
/// The proof has two halves that must both happen: the member is authenticated in the portal when the one-time code is made,
/// and the code then arrives as a WhatsApp message whose sender number the provider vouches for. A link, a screenshot or a
/// forwarded code on its own proves nothing, because completing needs a message from that number. Codes are single use, expire
/// in ten minutes, and are bound to the member who asked for them.
///
/// State lives on the existing Member row (Phone, PhoneNormalized, PhoneVerifiedAt) and in Redis (the pending code). There is
/// no link table.
/// </summary>
public class WhatsAppLinkService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<InstitutionAuditLogEntry> auditRepo,
    ICurrentTenantService tenant,
    IRedisService<MemberRedisConfig> redis,
    IOptions<WaSenderConfig> whatsApp,
    ILogger<WhatsAppLinkService> logger,
    ReservEase.Alumni.PostgresDb.Sdk.Services.IPlatformWhatsAppSwitch? platformSwitch = null) : IAssistantLinker
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";   // no 0/O/1/I/L: read aloud or typed from a screen
    private const int CodeLength = 8;

    private sealed class Challenge
    {
        public string MemberId { get; set; } = "";
        public string InstitutionId { get; set; } = "";
    }

    private sealed class PendingApproval
    {
        public string Sender { get; set; } = "";
        public string InstitutionId { get; set; } = "";
    }

    private static string PendingKey(string memberId) => $"wa:linkpend:{memberId}";
    private static string CodeKey(string code) => $"wa:link:{code}";
    private static string MemberKey(string memberId) => $"wa:linkm:{memberId}";

    // ── Portal side ──────────────────────────────────────────────────────

    public async Task<WhatsAppLinkChallenge> CreateChallengeAsync(string memberId)
    {
        // One live code per member: asking again retires the previous one.
        var previous = await redis.GetAsync<string>(MemberKey(memberId));
        if (!string.IsNullOrEmpty(previous)) await redis.RemoveAsync(CodeKey(previous));

        var code = NewCode();
        await redis.SetAsync(CodeKey(code), new Challenge { MemberId = memberId, InstitutionId = tenant.InstitutionId ?? "" }, CodeLifetime);
        await redis.SetAsync(MemberKey(memberId), code, CodeLifetime);

        var number = whatsApp.Value.AssistantNumber;
        var deepLink = string.IsNullOrWhiteSpace(number) ? null : $"https://wa.me/{number}?text={Uri.EscapeDataString($"LINK {code}")}";
        return new WhatsAppLinkChallenge(code, deepLink, (int)CodeLifetime.TotalSeconds, string.IsNullOrWhiteSpace(number) ? null : number);
    }

    public async Task<WhatsAppLinkStatus> GetStatusAsync(string memberId)
    {
        var config = whatsApp.Value;
        var member = await memberRepo.GetByIdAsync(memberId);
        var pref = await prefRepo.GetOneAsync(p => p.MemberId == memberId);
        var available = config.Enabled && !string.IsNullOrWhiteSpace(config.AssistantNumber)
            && (platformSwitch is null || await platformSwitch.IsOnAsync());
        var pending = await redis.GetAsync<PendingApproval>(PendingKey(memberId));

        if (member?.PhoneVerifiedAt is null)
            return new WhatsAppLinkStatus(available, false, null, null, string.IsNullOrWhiteSpace(config.AssistantNumber) ? null : config.AssistantNumber,
                pref?.WhatsAppAlerts ?? false, pending is null ? null : PhoneNumbers.Mask(pending.Sender));

        return new WhatsAppLinkStatus(available, true, PhoneNumbers.Mask(member.PhoneNormalized), member.PhoneVerifiedAt,
            string.IsNullOrWhiteSpace(config.AssistantNumber) ? null : config.AssistantNumber, pref?.WhatsAppAlerts ?? false);
    }

    /// <summary>Disconnects the signed-in member's own account. Takes effect at once for the assistant.</summary>
    public async Task<bool> UnlinkMemberAsync(string memberId)
    {
        var member = await memberRepo.GetByIdAsync(memberId);
        if (member?.PhoneVerifiedAt is null) return false;
        await Revoke(member, "member disconnected WhatsApp in the portal");
        return true;
    }

    // ── WhatsApp side ────────────────────────────────────────────────────

    public async Task<LinkOutcome> CompleteAsync(string code, string senderPhone, CancellationToken ct = default)
    {
        var sender = PhoneNumbers.Normalize(senderPhone);
        if (sender is null || string.IsNullOrWhiteSpace(code)) return LinkOutcome.Invalid;

        var key = CodeKey(code.Trim().ToUpperInvariant());
        var challenge = await redis.GetAsync<Challenge>(key);
        if (challenge is null) return LinkOutcome.Invalid;
        // Consumed before anything else: whatever happens next, this code never works a second time.
        await redis.RemoveAsync(key);
        await redis.RemoveAsync(MemberKey(challenge.MemberId));

        tenant.SetInstitutionId(challenge.InstitutionId);
        var member = await memberRepo.GetByIdAsync(challenge.MemberId);
        if (member is null || member.Status != "Active") return new LinkOutcome(LinkStatus.NotAllowed);

        // One verified number per member per institution. Never silently take it from someone else.
        var holder = await memberRepo.GetOneAsync(m => m.PhoneNormalized == sender && m.PhoneVerifiedAt != null && m.Id != member.Id && m.Status == "Active");
        if (holder is not null)
        {
            logger.LogWarning("WhatsApp link refused: number already verified on another member of institution {InstitutionId}", member.InstitutionId);
            return new LinkOutcome(LinkStatus.Conflict);
        }

        // A different number from the one on the account needs the member to say so in the portal: whoever is holding the code
        // may not be the member (it may have been forwarded to them).
        if (!PhoneNumbers.AreSame(member.Phone, sender))
        {
            await redis.SetAsync(PendingKey(member.Id), new PendingApproval { Sender = sender, InstitutionId = member.InstitutionId }, CodeLifetime);
            return new LinkOutcome(LinkStatus.NeedsApproval, await CommunityNameAsync(member), member.FirstName, member.InstitutionId);
        }

        return await ApplyAsync(member, sender);
    }

    /// <summary>The signed-in member approves the number that sent their code. Only they can: it needs their portal session.</summary>
    public async Task<LinkOutcome> ApproveAsync(string memberId)
    {
        var pending = await redis.GetAsync<PendingApproval>(PendingKey(memberId));
        if (pending is null) return LinkOutcome.Invalid;
        await redis.RemoveAsync(PendingKey(memberId));

        tenant.SetInstitutionId(pending.InstitutionId);
        var member = await memberRepo.GetByIdAsync(memberId);
        if (member is null || member.Status != "Active") return new LinkOutcome(LinkStatus.NotAllowed);

        var holder = await memberRepo.GetOneAsync(m => m.PhoneNormalized == pending.Sender && m.PhoneVerifiedAt != null && m.Id != member.Id && m.Status == "Active");
        if (holder is not null) return new LinkOutcome(LinkStatus.Conflict);

        return await ApplyAsync(member, pending.Sender);
    }

    /// <summary>The member says that number is not theirs. It is forgotten; nothing was connected.</summary>
    public async Task<bool> RejectPendingAsync(string memberId)
    {
        var pending = await redis.GetAsync<PendingApproval>(PendingKey(memberId));
        if (pending is null) return false;
        await redis.RemoveAsync(PendingKey(memberId));
        tenant.SetInstitutionId(pending.InstitutionId);
        var member = await memberRepo.GetByIdAsync(memberId);
        if (member is not null) await Audit(member, "WhatsApp connection refused by the member");
        return true;
    }

    private async Task<LinkOutcome> ApplyAsync(MemberEntity member, string sender)
    {
        member.Phone = $"+{sender}";           // the number WhatsApp itself reported
        member.PhoneVerifiedAt = DateTime.UtcNow;
        await memberRepo.UpdateAsync(member);
        await Audit(member, "WhatsApp connected");

        var alsoLinked = await LinkSiblingsAsync(member, sender);
        return new LinkOutcome(LinkStatus.Linked, await CommunityNameAsync(member), member.FirstName, member.InstitutionId, alsoLinked);
    }

    private async Task<string> CommunityNameAsync(MemberEntity member)
    {
        var institution = await institutionRepo.GetOneAsync(i => i.Id == member.InstitutionId, ignoreQueryFilters: true);
        return institution is null ? "your community" : (string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName);
    }

    public async Task<int> UnlinkAsync(string senderPhone, CancellationToken ct = default)
    {
        var sender = PhoneNumbers.Normalize(senderPhone);
        if (sender is null) return 0;

        var rows = (await memberRepo.GetAllAsync(m => m.PhoneNormalized == sender && m.PhoneVerifiedAt != null, ignoreQueryFilters: true)).ToList();
        foreach (var row in rows)
        {
            tenant.SetInstitutionId(row.InstitutionId);
            await Revoke(row, "member disconnected WhatsApp from WhatsApp");
        }
        return rows.Count;
    }

    public async Task<int> SetAlertsAsync(string senderPhone, bool on, CancellationToken ct = default)
    {
        var sender = PhoneNumbers.Normalize(senderPhone);
        if (sender is null) return 0;

        var rows = (await memberRepo.GetAllAsync(m => m.PhoneNormalized == sender && m.PhoneVerifiedAt != null && m.Status == "Active", ignoreQueryFilters: true)).ToList();
        foreach (var row in rows)
        {
            tenant.SetInstitutionId(row.InstitutionId);
            var pref = await prefRepo.GetOneAsync(p => p.MemberId == row.Id);
            if (pref is null)
            {
                if (!on) continue;   // nothing to switch off: alerts were never on
                pref = new NotificationPreference { InstitutionId = row.InstitutionId, MemberId = row.Id, WhatsAppAlerts = true, CreatedBy = row.Id };
                await prefRepo.AddAsync(pref);
            }
            else if (pref.WhatsAppAlerts != on)
            {
                pref.WhatsAppAlerts = on;
                await prefRepo.UpdateAsync(pref);
            }
            await Audit(row, on ? "WhatsApp alerts turned on by message" : "WhatsApp alerts turned off by message");
        }
        return rows.Count;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// The same person in other institutions, recognised by an email that was verified by one-time code there: their number is
    /// proved too, so they need not link once per community. Only when that account has no number or already has this one; a
    /// different number is left alone, and so is any number already verified on someone else.
    /// </summary>
    private async Task<int> LinkSiblingsAsync(MemberEntity linked, string sender)
    {
        if (!linked.IsEmailVerified || string.IsNullOrWhiteSpace(linked.Email)) return 0;
        var email = linked.Email.ToLowerInvariant();

        var siblings = (await memberRepo.GetAllAsync(
            m => m.Email.ToLower() == email && m.InstitutionId != linked.InstitutionId && m.IsEmailVerified && m.Status == "Active" && m.PhoneVerifiedAt == null,
            ignoreQueryFilters: true)).ToList();

        var count = 0;
        foreach (var s in siblings)
        {
            var sameNumber = s.PhoneNormalized == sender;
            if (!sameNumber && !string.IsNullOrWhiteSpace(s.Phone)) continue;

            tenant.SetInstitutionId(s.InstitutionId);
            var taken = await memberRepo.GetOneAsync(m => m.PhoneNormalized == sender && m.PhoneVerifiedAt != null && m.Id != s.Id && m.Status == "Active");
            if (taken is not null) continue;

            if (!sameNumber) s.Phone = $"+{sender}";
            s.PhoneVerifiedAt = DateTime.UtcNow;
            await memberRepo.UpdateAsync(s);
            await Audit(s, "WhatsApp connected (same verified email)");
            count++;
        }
        return count;
    }

    private async Task Revoke(MemberEntity member, string reason)
    {
        member.PhoneVerifiedAt = null;
        await memberRepo.UpdateAsync(member);

        // The notification consent goes with the connection: nothing is sent to a number that is no longer proved.
        var pref = await prefRepo.GetOneAsync(p => p.MemberId == member.Id);
        if (pref is { WhatsAppAlerts: true })
        {
            pref.WhatsAppAlerts = false;
            await prefRepo.UpdateAsync(pref);
        }

        await Audit(member, $"WhatsApp disconnected ({reason})");
    }

    private async Task Audit(MemberEntity member, string action)
        => await auditRepo.AddAsync(new InstitutionAuditLogEntry
        {
            InstitutionId = member.InstitutionId,
            ActorId = member.Id,
            Actor = $"{member.FirstName} {member.LastName}".Trim(),
            Action = action,
            Target = "WhatsApp",
            CreatedBy = member.Id,
        });

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
