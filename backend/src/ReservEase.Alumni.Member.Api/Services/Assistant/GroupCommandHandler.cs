using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>
/// What the assistant does inside WhatsApp groups. Deliberately narrow:
///
/// - Only a message that starts with "!" is looked at. Ordinary conversation is never read, kept or answered.
/// - A group is answered only when an administrator connected it (code, then confirmation in the portal). Any other group is
///   met with silence, so the assistant being added somewhere does nothing.
/// - Which institution a group belongs to comes only from that mapping, never from who is speaking.
/// - Replies carry public information only. A group has many people in it and no way to tell who is a member, so nothing
///   personal is ever said here; personal requests are sent to the private chat.
/// </summary>
public class GroupCommandHandler(
    IAlumniPgRepository<Institution> institutionRepo,
    IAssistantDirectory directory,
    IAssistantContent content,
    IWhatsAppProvider provider,
    IRedisService<MemberRedisConfig> redis,
    IOptions<WaSenderConfig> whatsApp)
{
    private const int MaxCommandsPerMinute = 10;
    private const int MaxItems = 3;
    private static readonly TimeSpan RouteCacheLifetime = TimeSpan.FromSeconds(60);
    // Short, so a group an administrator has just confirmed starts answering almost at once.
    private static readonly TimeSpan MissCacheLifetime = TimeSpan.FromSeconds(10);

    private sealed class GroupRoute { public string? InstitutionId { get; set; } public string? LinkId { get; set; } }
    private sealed class GroupRate { public DateTime WindowStartedAt { get; set; } = DateTime.UtcNow; public int Count { get; set; } }

    public async Task<AssistantReply?> HandleAsync(WhatsAppInboundEvent message, CancellationToken ct = default)
    {
        if (message.Kind != WhatsAppInboundKind.GroupMessage || message.FromMe || message.ChatId is null) return null;
        var text = message.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text[0] != '!') return null;

        var parts = text[1..].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;
        var command = parts[0].ToLowerInvariant();
        var argument = parts.Length > 1 ? parts[1] : "";

        if (command == "link") return await TryLinkAsync(message.ChatId, argument, ct);

        var route = await FindRouteAsync(message.ChatId, ct);
        if (route?.InstitutionId is null) return null;   // not a connected group: say nothing at all

        var institution = await institutionRepo.GetOneAsync(i => i.Id == route.InstitutionId && i.Status == "Active", ignoreQueryFilters: true);
        var link = institution?.WhatsAppGroups.FirstOrDefault(g => g.Id == route.LinkId && g.Status == WhatsAppGroupStatuses.Active && g.GroupJid == message.ChatId);
        if (institution is null || link is null || !link.AssistantEnabled) return null;
        if (institution.DisabledFeatures.Contains(InstitutionFeatures.WhatsApp)) return null;
        if (link.AllowedCommands.Count > 0 && !link.AllowedCommands.Contains(command, StringComparer.OrdinalIgnoreCase)) return null;

        var rate = await CountAsync(message.ChatId);
        if (rate > MaxCommandsPerMinute) return null;
        if (rate == MaxCommandsPerMinute) return new AssistantReply(message.ChatId, "That's a lot of commands. Please give it a minute.");

        var community = await directory.FindByIdAsync(institution.Id, ct);
        if (community is null) return null;

        var privateChat = string.IsNullOrWhiteSpace(whatsApp.Value.AssistantNumber)
            ? ""
            : $"\nFor anything personal, message me privately: https://wa.me/{whatsApp.Value.AssistantNumber}?text=start%20{institution.Slug}";

        var reply = command switch
        {
            "help" => Help(community.Name, link) + privateChat,
            "events" => await Listing("Upcoming events", await Allowed(community, InstitutionFeatures.Events, ct, () => content.PublicEventsAsync(community, ct)), community, "events", "No upcoming events right now.") + privateChat,
            "campaigns" => await Listing("Fundraisers", await Allowed(community, InstitutionFeatures.Contributions, ct, () => content.PublicCampaignsAsync(community, ct)), community, "contributions", "No public fundraisers right now.") + privateChat,
            "jobs" => await Listing("Jobs and opportunities", await Allowed(community, InstitutionFeatures.Jobs, ct, () => content.PublicJobsAsync(community, ct)), community, "jobs", "No open opportunities right now.") + privateChat,
            "updates" => await Listing("Community updates", await Allowed(community, InstitutionFeatures.News, ct, () => content.PublicUpdatesAsync(community, ct)), community, "news", "Nothing new right now.") + privateChat,
            "community" => CommunityLine(community, link) + privateChat,
            _ => $"I don't know *!{command}*. Try *!help*.",
        };
        return new AssistantReply(message.ChatId, reply);
    }

    // ── Connecting a group ───────────────────────────────────────────────

    /// <summary>
    /// "!link CODE": the code an administrator generated in the portal. Whoever posts it only tells us which group to propose;
    /// the group does nothing until that administrator confirms it. A wrong or expired code is met with silence.
    /// </summary>
    private async Task<AssistantReply?> TryLinkAsync(string groupJid, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var wanted = code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var institutions = await institutionRepo.GetQueryable(i => i.Status == "Active", ignoreQueryFilters: true).ToListAsync(ct);
        var already = institutions.SelectMany(i => i.WhatsAppGroups).Any(g => g.GroupJid == groupJid && g.Status != WhatsAppGroupStatuses.AwaitingGroup);
        if (already) return new AssistantReply(groupJid, "This group is already connected to AlumUnion.");

        foreach (var institution in institutions)
        {
            var link = institution.WhatsAppGroups.FirstOrDefault(g =>
                g.Status == WhatsAppGroupStatuses.AwaitingGroup && g.Code == wanted && g.CodeExpiresAt > now);
            if (link is null) continue;

            var group = await provider.GetGroupAsync(groupJid, ct);
            link.GroupJid = groupJid;
            link.Status = WhatsAppGroupStatuses.Detected;
            link.Code = null;
            link.CodeExpiresAt = null;
            if (!string.IsNullOrWhiteSpace(group?.Name)) link.Name = group.Name;

            // The collection is replaced so change tracking sees the update.
            institution.WhatsAppGroups = [.. institution.WhatsAppGroups];
            await institutionRepo.UpdateAsync(institution);
            await redis.RemoveAsync(RouteKey(groupJid));

            return new AssistantReply(groupJid,
                "Got it. An administrator of this community now needs to confirm this group in the AlumUnion portal. Once they have, send *!help* here to see what I can do.");
        }
        return null;
    }

    // ── Finding a connected group ────────────────────────────────────────

    private async Task<GroupRoute?> FindRouteAsync(string groupJid, CancellationToken ct)
    {
        var cached = await redis.GetAsync<GroupRoute>(RouteKey(groupJid));
        if (cached is not null) return cached;

        var institutions = await institutionRepo.GetQueryable(i => i.Status == "Active", ignoreQueryFilters: true).ToListAsync(ct);
        var hit = institutions
            .SelectMany(i => i.WhatsAppGroups.Where(g => g.GroupJid == groupJid && g.Status == WhatsAppGroupStatuses.Active).Select(g => (i, g)))
            .FirstOrDefault();

        // Misses are cached too: the assistant may sit in many unrelated groups, and each "!" message would otherwise scan every institution.
        var route = new GroupRoute { InstitutionId = hit.i?.Id, LinkId = hit.g?.Id };
        await redis.SetAsync(RouteKey(groupJid), route, hit.i is null ? MissCacheLifetime : RouteCacheLifetime);
        return route;
    }

    private async Task<int> CountAsync(string groupJid)
    {
        var key = $"wa:grp:rate:{groupJid}";
        var rate = await redis.GetAsync<GroupRate>(key) ?? new GroupRate();
        if (DateTime.UtcNow - rate.WindowStartedAt > TimeSpan.FromMinutes(1)) rate = new GroupRate();
        rate.Count++;
        await redis.SetAsync(key, rate, TimeSpan.FromMinutes(2));
        return rate.Count;
    }

    private static string RouteKey(string groupJid) => $"wa:grp:route:{groupJid}";

    // ── Text ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<AssistantItem>> Allowed(AssistantCommunity c, string feature, CancellationToken ct, Func<Task<IReadOnlyList<AssistantItem>>> load)
        => await content.IsAvailableAsync(c, feature, ct) ? await load() : [];

    private static Task<string> Listing(string title, IReadOnlyList<AssistantItem> items, AssistantCommunity c, string path, string empty)
    {
        var sb = new StringBuilder($"*{title}* · {c.Name}\n");
        if (items.Count == 0) sb.AppendLine(empty);
        foreach (var (item, i) in items.Take(MaxItems).Select((x, i) => (x, i)))
        {
            sb.AppendLine($"{i + 1}. {item.Title}");
            if (!string.IsNullOrWhiteSpace(item.Detail)) sb.AppendLine($"   {item.Detail}");
            sb.AppendLine($"   {item.Url}");
        }
        if (items.Count > MaxItems) sb.AppendLine($"More: {c.PortalUrl}/{path}");
        return Task.FromResult(sb.ToString().TrimEnd());
    }

    private static string Help(string name, WhatsAppGroupLink link)
    {
        var all = link.AllowedCommands.Count > 0 ? link.AllowedCommands : WhatsAppGroupCommands.All.ToList();
        var lines = new[]
        {
            ("events", "!events: upcoming events"), ("campaigns", "!campaigns: open fundraisers"), ("jobs", "!jobs: jobs and opportunities"),
            ("updates", "!updates: community news"), ("community", "!community: where to find us"), ("help", "!help: this list"),
        }.Where(l => all.Contains(l.Item1, StringComparer.OrdinalIgnoreCase)).Select(l => l.Item2);
        return $"*{name} on AlumUnion*\n{string.Join("\n", lines)}";
    }

    private static string CommunityLine(AssistantCommunity c, WhatsAppGroupLink link)
        => $"*{c.Name}*\nPortal: {c.PortalUrl}\nJoin: {c.PortalUrl}/register";
}
