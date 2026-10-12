using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>
/// The deterministic WhatsApp assistant: a numbered menu over what AlumUnion already knows, with no language model. It decides
/// who is asking from the provider's own sender number (never from message text), which community they mean, and what that
/// person may see, then answers with short text and exact links into the member portal.
///
/// Guests (a number with no verified member behind it) get only what an anonymous portal visitor could see. A verified member
/// gets what the portal itself would show them, by way of <see cref="IAssistantContent"/>. Nothing is stored beyond a half-hour
/// conversation state.
/// </summary>
public class AssistantEngine(
    IAssistantDirectory directory,
    IAssistantContent content,
    IAssistantStateStore stateStore,
    IAssistantLinker linker,
    IAssistantActions actions,
    IOptions<WaSenderConfig> whatsApp,
    ILogger<AssistantEngine>? logger = null)
{
    private const int MaxListItems = 5;
    private static readonly Regex LinkCode = new(@"^link\s+([a-z2-9]{6,10})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const int MaxLinkFailures = 5;
    private const int MaxNoteLength = 500;
    private static readonly Regex Rsvp = new(@"^(rsvp|book|attend|going)\s+(\d{1,2})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CancelRsvp = new(@"^(unrsvp|cancel rsvp)\s+(\d{1,2})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { "stop", "stop alerts", "stop notifications", "stop all", "unsubscribe", "opt out", "optout", "mute", "no more messages" };
    private static readonly HashSet<string> ResumeWords = new(StringComparer.OrdinalIgnoreCase)
        { "start alerts", "alerts on", "resume alerts", "resume", "subscribe", "unmute", "turn on alerts" };
    private static readonly Regex Greeting = new(@"^(hi|hello|hey|hola|menu|start|home|good (morning|afternoon|evening)|yo|salaam)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<AssistantReply?> HandleAsync(WhatsAppInboundEvent message, CancellationToken ct = default)
    {
        if (message.Kind != WhatsAppInboundKind.PersonalMessage || message.FromMe) return null;
        var to = message.ReplyTo;
        if (to is null) return null;

        var phone = PhoneNumbers.Normalize(message.SenderPhone);
        if (phone is null)
            return new AssistantReply(to, "I couldn't read your WhatsApp number, so I can't tell who you are. Please try again from the phone's own WhatsApp.");

        var state = await stateStore.GetAsync(phone) ?? new AssistantState();

        // A redelivered webhook is the same message: answering (or acting on) it twice would be wrong.
        if (message.MessageId is not null && message.MessageId == state.LastMessageId) return null;
        state.LastMessageId = message.MessageId;

        // Voice notes, pictures, locations: say plainly what works instead of a generic "didn't understand".
        if (string.IsNullOrWhiteSpace(message.Text) && message.MediaKind is not null)
        {
            await stateStore.SetAsync(phone, state);
            return new AssistantReply(to, MediaReply(message.MediaKind));
        }

        var text = Clean(message.Text);

        // "!link CODE", "!events"... are for a group. Typed here they would otherwise be read as a personal "link" and refused
        // with a message about expired codes, which sends people looking in the wrong place.
        if (message.Text?.TrimStart().StartsWith('!') == true)
        {
            await stateStore.SetAsync(phone, state);
            return new AssistantReply(to, GroupCommandInPrivate);
        }

        string reply;
        try
        {
            var communities = await directory.FindMemberCommunitiesAsync(phone, ct);
            reply = await RouteAsync(text, (message.Text ?? "").Trim(), phone, state, communities, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Never leave someone talking to silence: say so, and keep where they were so "menu" still works.
            logger?.LogError(e, "WhatsApp assistant failed answering a message (stage {Stage})", state.Stage);
            reply = "Sorry, I couldn't get that just now. Please try again, or reply *menu*.";
        }

        await stateStore.SetAsync(phone, state);
        return new AssistantReply(to, reply);
    }

    // ── Routing ──────────────────────────────────────────────────────────

    private async Task<string> RouteAsync(string text, string raw, string phone, AssistantState state, IReadOnlyList<AssistantCommunity> mine, CancellationToken ct)
    {
        var linkReply = await TryLinkCommandsAsync(text, phone, state, ct);
        if (linkReply is not null) return linkReply;

        // "start <code>" arrives from a "Chat with AlumUnion" link on an institution's own portal, naming the community.
        if (text.StartsWith("start ") && text.Length > 6)
        {
            var named = await directory.FindBySlugAsync(text[6..].Trim(), ct);
            if (named is not null) return await Select(named, state, mine, ct);
        }

        var current = await ResolveCurrentAsync(state, mine, ct);

        // Typing "stop" is how people say no: it must always work, from any point, and say what it did.
        if (StopWords.Contains(text))
        {
            ClearFlow(state);
            var off = await linker.SetAlertsAsync(phone, false, ct);
            return off == 0
                ? "No alerts are being sent to this number."
                : "Done. WhatsApp alerts are off.\n\nYou can still ask me things here, and you'll get alerts by SMS or email as your portal settings allow.\n\nReply *alerts on* to turn them back on, or *unlink* to disconnect this number.";
        }
        if (ResumeWords.Contains(text) && state.Flow is null)
        {
            var on = await linker.SetAlertsAsync(phone, true, ct);
            return on == 0
                ? "To get alerts here, connect your account first: reply *link*."
                : "Done. WhatsApp alerts are on again.\n\nReply *stop* any time to switch them off.";
        }

        if (text is "help" or "?") return Help(current);

        if (text is "back" or "cancel" or "menu" or "0" || Greeting.IsMatch(text))
        {
            var leaving = state.Flow is not null;
            ClearFlow(state);
            state.Stage = AssistantStages.Menu;
            if (leaving && current is not null) return $"Okay, I've dropped that. Nothing was sent.\n\n{Menu(current)}";
            return current is null ? await AskCommunityAsync(state, mine, ct) : Menu(current);
        }

        // A guided flow owns the next message: what the member types is an answer, not a command.
        if (state.Flow is not null && current is { IsMember: true })
            return await ContinueFlowAsync(text, raw, state, current, ct);

        var rsvp = Rsvp.Match(text);
        if (rsvp.Success && current is not null) return await RsvpAsync(int.Parse(rsvp.Groups[2].Value), cancel: false, state, current, ct);
        var cancelRsvp = CancelRsvp.Match(text);
        if (cancelRsvp.Success && current is not null) return await RsvpAsync(int.Parse(cancelRsvp.Groups[2].Value), cancel: true, state, current, ct);

        if (current is { IsMember: true } && text is "suggest" or "suggest job" or "post job" or "share opportunity" or "share job")
            return StartFlow("suggest", state);
        if (current is { IsMember: true } && text is "post update" or "post" or "share update" or "update")
            return StartFlow("note", state);

        // "6" is "Switch Community" only on the menu; in a numbered pick list it is that list's sixth entry (handled below).
        if (text is "switch" or "change community" or "change" || (text == "6" && current is not null && state.Stage == AssistantStages.Menu))
            return await AskCommunityAsync(state, mine, ct);

        if (text is "link" or "connect" or "link my account")
            return LinkInstructions(current ?? mine.FirstOrDefault());

        // Two shortcuts people reach for without opening the menu.
        if (current is not null && text is "pay" or "dues" or "give" or "donate" or "contribute")
        {
            if (!current.IsMember) state.Wanted = 1;
            return await SectionAsync(1, current, state, ct);
        }
        if (current is not null && text is "invite" or "refer" or "share" or "invite a friend" or "invite friends")
            return await InviteAsync(current, ct);

        // A numbered answer to the list the user was last shown.
        if (state.Stage == AssistantStages.PickCommunity && int.TryParse(text, out var pick) && pick >= 1 && pick <= state.Choices.Count)
        {
            var chosen = mine.FirstOrDefault(m => m.InstitutionId == state.Choices[pick - 1]) ?? await directory.FindByIdAsync(state.Choices[pick - 1], ct);
            if (chosen is not null) return await Select(chosen, state, mine, ct);
        }

        // Without a community there is nothing to talk about yet: treat what they typed as the community's name.
        if (current is null) return await SearchOrAskAsync(text, state, mine, ct);

        if (state.Stage == AssistantStages.Share && current.IsMember && text is "1" or "2")
            return StartFlow(text == "1" ? "suggest" : "note", state);

        if ((state.Stage == AssistantStages.Menu || state.Stage == AssistantStages.Share) && int.TryParse(text, out var option))
        {
            // A guest asking for something personal gets the public part now and the personal part as soon as they connect.
            if (!current.IsMember && option is 1 or 5) state.Wanted = option;
            return await SectionAsync(option, current, state, ct);
        }

        return $"I didn't catch that. Reply with a number from the menu, or *menu* to see it again.\n\n{Menu(current)}";
    }

    /// <summary>
    /// "LINK CODE" completes an account connection started in the portal; "unlink" removes it. The sender number comes from the
    /// provider event, so only the person holding that WhatsApp account can complete a code. Repeated wrong codes pause linking.
    /// </summary>
    private async Task<string?> TryLinkCommandsAsync(string text, string phone, AssistantState state, CancellationToken ct)
    {
        if (text is "unlink" or "disconnect" or "unlink my account")
        {
            var removed = await linker.UnlinkAsync(phone, ct);
            state.InstitutionId = null;
            state.Stage = AssistantStages.AskCommunity;
            return removed == 0
                ? "This number isn't connected to an AlumUnion account."
                : $"Done. This WhatsApp number is no longer connected{(removed > 1 ? $" ({removed} accounts)" : "")}, and I won't send you notifications here. You can connect again any time with *link*.";
        }

        var m = LinkCode.Match(text);
        if (!m.Success) return null;

        if (state.LinkFailures >= MaxLinkFailures && DateTime.UtcNow - state.LinkFailureAt < TimeSpan.FromMinutes(15))
            return "Too many wrong codes. Please wait 15 minutes, then start again from the portal.";

        var outcome = await linker.CompleteAsync(m.Groups[1].Value, phone, ct);
        switch (outcome.Status)
        {
            case LinkStatus.Linked:
                state.LinkFailures = 0;
                state.InstitutionId = outcome.InstitutionId;
                state.Stage = AssistantStages.Menu;
                var extra = outcome.AlsoLinked > 0 ? $" Your other AlumUnion communities with the same email are connected too." : "";
                var connected = $"You're connected{(string.IsNullOrWhiteSpace(outcome.FirstName) ? "" : $", {outcome.FirstName}")}. This WhatsApp number is now linked to your {outcome.CommunityName} account.{extra}";
                // Carry on with what they were asking for before they had to connect.
                if (state.Wanted is { } wanted)
                {
                    state.Wanted = null;
                    var nowMine = (await directory.FindMemberCommunitiesAsync(phone, ct)).FirstOrDefault(c => c.InstitutionId == outcome.InstitutionId && c.IsMember);
                    if (nowMine is not null)
                        return $"{connected}\n\nHere is what you asked for:\n\n{await SectionAsync(wanted, nowMine, state, ct)}";
                }
                // Land them on the menu: they have just connected, so show what is now possible rather than ask them to ask.
                var landed = (await directory.FindMemberCommunitiesAsync(phone, ct)).FirstOrDefault(c => c.InstitutionId == outcome.InstitutionId && c.IsMember);
                return landed is null ? connected : $"{connected}\n\n{Menu(landed)}";
            case LinkStatus.NeedsApproval:
                // The code was right, but this is not the number on the account. Say what to do, and nothing about whose account it is.
                return $"Almost done. For your safety, open the *Connect WhatsApp* page in your portal ({outcome.CommunityName}) and approve this number. It takes one tap.";
            case LinkStatus.Conflict:
                return "This number is already connected to a different account in that community, so I can't connect it again. Please contact your community's admin for help.";
            case LinkStatus.NotAllowed:
                return "That account can't be connected right now. Please contact your community's admin.";
            default:
                state.LinkFailures++;
                state.LinkFailureAt = DateTime.UtcNow;
                return "That code didn't work. It may have expired (codes last 10 minutes) or been used already. Open your portal's *Connect WhatsApp* page to get a new one.\n\nIf it's a code for a WhatsApp *group*, post it inside the group as *!link CODE* instead of sending it to me here.";
        }
    }

    private async Task<AssistantCommunity?> ResolveCurrentAsync(AssistantState state, IReadOnlyList<AssistantCommunity> mine, CancellationToken ct)
    {
        if (state.InstitutionId is not null)
        {
            var existing = mine.FirstOrDefault(m => m.InstitutionId == state.InstitutionId);
            if (existing is not null) return existing;
            var pub = await directory.FindByIdAsync(state.InstitutionId, ct);
            if (pub is not null) return pub;
            state.InstitutionId = null;
        }

        // Exactly one community for this number: nothing to ask.
        var distinct = mine.GroupBy(m => m.InstitutionId).Select(g => g.First()).ToList();
        if (distinct.Count == 1)
        {
            state.InstitutionId = distinct[0].InstitutionId;
            return distinct[0];
        }
        return null;
    }

    private async Task<string> Select(AssistantCommunity chosen, AssistantState state, IReadOnlyList<AssistantCommunity> mine, CancellationToken ct)
    {
        // The directory's answer is public; whether this number is a member of it comes from its own matches.
        var asMember = mine.FirstOrDefault(m => m.InstitutionId == chosen.InstitutionId);
        var community = asMember ?? chosen;
        if (!await content.IsAvailableAsync(community, InstitutionFeatures.WhatsApp, ct))
        {
            state.InstitutionId = null;
            state.Stage = AssistantStages.AskCommunity;
            return $"{community.Name} hasn't turned on the WhatsApp assistant. You can still use the portal: {community.PortalUrl}";
        }

        state.InstitutionId = community.InstitutionId;
        state.Stage = AssistantStages.Menu;
        state.Choices = [];
        return Menu(community);
    }

    private async Task<string> AskCommunityAsync(AssistantState state, IReadOnlyList<AssistantCommunity> mine, CancellationToken ct)
    {
        var options = mine.GroupBy(m => m.InstitutionId).Select(g => g.First()).Take(9).ToList();
        if (options.Count == 0)
        {
            state.Stage = AssistantStages.AskCommunity;
            state.Choices = [];
            return "Welcome to the *AlumUnion Assistant*.\n\nWhich community are you looking for? Reply with its name, for example *UMaT Alumni*.";
        }

        state.Stage = AssistantStages.PickCommunity;
        state.Choices = options.Select(o => o.InstitutionId).ToList();
        var sb = new StringBuilder("*Which community?*\n\n");
        for (var i = 0; i < options.Count; i++) sb.AppendLine($"{i + 1}. {options[i].Name}");
        sb.Append("\nReply with a number.");
        return sb.ToString();
    }

    private async Task<string> SearchOrAskAsync(string text, AssistantState state, IReadOnlyList<AssistantCommunity> mine, CancellationToken ct)
    {
        if (mine.Count > 0 && state.Stage != AssistantStages.AskCommunity) return await AskCommunityAsync(state, mine, ct);
        if (text.Length < 3 || Greeting.IsMatch(text)) return await AskCommunityAsync(state, mine, ct);

        var found = await directory.SearchAsync(text, ct);
        if (found.Count == 0)
        {
            state.Stage = AssistantStages.AskCommunity;
            return "I couldn't find a community with that name. Check the spelling and try again, or ask your community's admin for their AlumUnion link.";
        }
        if (found.Count == 1) return await Select(found[0], state, mine, ct);

        state.Stage = AssistantStages.PickCommunity;
        state.Choices = found.Select(f => f.InstitutionId).ToList();
        var sb = new StringBuilder("*I found these. Which one?*\n\n");
        for (var i = 0; i < found.Count; i++) sb.AppendLine($"{i + 1}. {found[i].Name}");
        sb.Append("\nReply with a number.");
        return sb.ToString();
    }

    // ── Sections ─────────────────────────────────────────────────────────

    private async Task<string> SectionAsync(int option, AssistantCommunity c, AssistantState state, CancellationToken ct)
    {
        switch (option)
        {
            case 1:
                if (!await content.IsAvailableAsync(c, InstitutionFeatures.Contributions, ct)) return NotAvailable(c, "Contributions");
                return await ContributionsAsync(c, ct);
            case 2:
                if (!await content.IsAvailableAsync(c, InstitutionFeatures.Events, ct)) return NotAvailable(c, "Events");
                if (!c.IsMember)
                {
                    var open = await content.PublicEventsAsync(c, ct);
                    var list = List("Upcoming events", open, "No upcoming events right now.", c, "events");
                    return open.Count == 0 ? list : $"{list}\n\nTo book a place, connect your account: reply *link*.";
                }
                var events = await content.MemberEventsAsync(c, ct);
                state.EventIds = events.Take(MaxListItems).Select(e => e.Id ?? "").ToList();
                state.EventsInstitutionId = c.InstitutionId;
                var shown = List("Upcoming events", events, "No upcoming events right now.", c, "events");
                return events.Count == 0 ? shown
                    : $"{shown}\n\nTo book a place, reply *rsvp 1* (the event's number).";
            case 3:
                if (!await content.IsAvailableAsync(c, InstitutionFeatures.Jobs, ct)) return NotAvailable(c, "Jobs");
                return List("Jobs and opportunities", c.IsMember ? await content.MemberJobsAsync(c, ct) : await content.PublicJobsAsync(c, ct), "No open opportunities right now.", c, "jobs");
            case 4:
                if (!await content.IsAvailableAsync(c, InstitutionFeatures.News, ct)) return NotAvailable(c, "Community updates");
                return List("Community updates", c.IsMember ? await content.MemberUpdatesAsync(c, ct) : await content.PublicUpdatesAsync(c, ct), "Nothing new to share right now.", c, "news");
            case 5:
                return MyCommunity(c);
            case 7:
                return Help(c);
            case 8 when c.IsMember:
                state.Stage = AssistantStages.Share;
                return "*What would you like to share?*\n\n1️⃣  A job or opportunity\n      _an administrator reviews it first_\n2️⃣  An update for your year group's wall\n\nReply with a number, or *menu* to go back.";
            default:
                return $"That's not one of the options. Please choose a number from 1 to {(c.IsMember ? 8 : 7)}.\n\n{Menu(c)}";
        }
    }

    private async Task<string> ContributionsAsync(AssistantCommunity c, CancellationToken ct)
    {
        var sb = new StringBuilder();
        if (c.IsMember)
        {
            var dues = await content.MemberDuesAsync(c, ct);
            if (dues is not null)
            {
                sb.AppendLine($"*Your dues at {c.Name}*");
                sb.AppendLine(dues.CurrentYearPaid && !dues.HasArrears
                    ? "You're up to date. Thank you."
                    : dues.HasArrears
                        ? $"You have {dues.UnpaidCount} unpaid {(dues.UnpaidCount == 1 ? "year" : "years")} of dues."
                        : "This year's dues are not paid yet.");
                if (!dues.CurrentYearPaid || dues.HasArrears) sb.AppendLine($"Pay here: {dues.Url}");
                sb.AppendLine();
            }
            sb.Append(List("Fundraisers you can support", await content.MemberCampaignsAsync(c, ct), "No active fundraisers right now.", c, "contributions"));
        }
        else
        {
            sb.Append(List("Fundraisers", await content.PublicCampaignsAsync(c, ct), "No public fundraisers right now.", c, "contributions"));
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("To see your own dues, connect your account: reply *link*.");
        }
        return sb.ToString().TrimEnd();
    }

    private string MyCommunity(AssistantCommunity c)
    {
        if (!c.IsMember)
            return $"*{c.Name}*\n\nYou're browsing as a guest.\nPortal: {c.PortalUrl}\n\nTo see your own details here, connect your account: reply *link*.";
        var name = string.IsNullOrWhiteSpace(c.MemberFirstName) ? "" : $", {c.MemberFirstName}";
        return $"*{c.Name}*\n\nYou're connected{name}.\nPortal: {c.PortalUrl}\nProfile: {c.PortalUrl}/profile";
    }

    // ── Text ─────────────────────────────────────────────────────────────

    private string Menu(AssistantCommunity c)
    {
        var who = c.IsMember
            ? $"Welcome back{(string.IsNullOrWhiteSpace(c.MemberFirstName) ? "" : $", {c.MemberFirstName}")}."
            : "You're browsing as a guest.";
        var sb = new StringBuilder();
        sb.AppendLine($"*{c.Name}*");
        sb.AppendLine("AlumUnion Assistant");
        sb.AppendLine();
        sb.AppendLine(who);
        sb.AppendLine();
        sb.AppendLine("1️⃣  Contributions & dues");
        sb.AppendLine("2️⃣  Events");
        sb.AppendLine("3️⃣  Jobs & opportunities");
        sb.AppendLine("4️⃣  Community updates");
        sb.AppendLine("5️⃣  My community");
        sb.AppendLine("6️⃣  Switch community");
        sb.AppendLine("7️⃣  Help");
        if (c.IsMember) sb.AppendLine("8️⃣  Share a job or an update");
        sb.AppendLine();
        sb.Append("Reply with a number.");
        return sb.ToString();
    }

    private string Help(AssistantCommunity? c)
    {
        var sb = new StringBuilder("*How I can help*\n\n");
        sb.AppendLine("*menu*  the main menu");
        sb.AppendLine("*back*  go back");
        sb.AppendLine("*switch*  change community");
        if (c is { IsMember: true })
        {
            sb.AppendLine("*pay*  your dues and fundraisers");
            sb.AppendLine("*invite*  invite a friend");
            sb.AppendLine("*rsvp 1*  book an event you were shown");
            sb.AppendLine("*suggest*  send a job or opportunity");
            sb.AppendLine("*post update*  post to your year group");
            sb.AppendLine("*stop*  pause WhatsApp alerts");
            sb.AppendLine("*unlink*  disconnect this number");
        }
        else
        {
            sb.AppendLine("*link*  connect your account");
        }
        sb.AppendLine();
        sb.Append(c is { IsMember: true }
            ? "I never ask for your password or payment details here."
            : "Until you connect your account I only show what's open to everyone. I never ask for your password or payment details here.");
        if (c is not null) sb.Append($"\n\nYour portal: {c.PortalUrl}");
        return sb.ToString();
    }

    private string LinkInstructions(AssistantCommunity? c)
    {
        if (c is null)
            return "To connect your account, first tell me your community: reply with its name.";
        return $"*Connect your {c.Name} account*\n\n1. Open {c.PortalUrl}/connect-whatsapp and sign in.\n2. Tap *Open WhatsApp*. It brings you back here with a one-time code typed in.\n3. Send it as it is.\n\nThat proves this number is yours. The code works once and lasts 10 minutes.";
    }

    private string NotAvailable(AssistantCommunity c, string what)
        => $"{what} isn't available for {c.Name} right now.\n\n{Menu(c)}";

    private static string List(string title, IReadOnlyList<AssistantItem> items, string empty, AssistantCommunity c, string portalPath, bool footer = false)
    {
        var sb = new StringBuilder($"*{title}*\n");
        if (items.Count == 0) sb.AppendLine($"\n{empty}");
        for (var i = 0; i < Math.Min(items.Count, MaxListItems); i++)
        {
            sb.AppendLine();
            sb.AppendLine($"{i + 1}. *{items[i].Title}*");
            if (!string.IsNullOrWhiteSpace(items[i].Detail)) sb.AppendLine($"    {items[i].Detail}");
            sb.AppendLine($"    {items[i].Url}");
        }
        if (items.Count > MaxListItems) sb.AppendLine($"\nSee everything: {c.PortalUrl}/{portalPath}");
        return sb.ToString().TrimEnd();
    }

    // ── Doing things: RSVP and guided flows ──────────────────────────────

    private async Task<string> RsvpAsync(int number, bool cancel, AssistantState state, AssistantCommunity c, CancellationToken ct)
    {
        if (!c.IsMember) return "To RSVP I need to know it's you. Reply *link* to connect your account.";
        if (state.EventsInstitutionId != c.InstitutionId || number < 1 || number > state.EventIds.Count || string.IsNullOrEmpty(state.EventIds[number - 1]))
            return "Reply *2* to see the events first, then use the number next to the event, for example *rsvp 1*.";

        var id = state.EventIds[number - 1];
        var result = cancel ? await actions.CancelRsvpAsync(c, id, ct) : await actions.RsvpAsync(c, id, ct);
        return result.Message;
    }

    /// <summary>A message the member can forward as it is, carrying their own invitation link.</summary>
    private async Task<string> InviteAsync(AssistantCommunity c, CancellationToken ct)
    {
        if (!c.IsMember)
            return $"Inviting friends uses your own link, so I need to know it's you first. Reply *link* to connect your account.";

        var link = await content.MemberInviteLinkAsync(c, ct);
        if (link is null)
            return $"I couldn't make your invitation link just now. You can find it on the Refer & Earn page: {c.PortalUrl}/referrals";

        return $"*Invite a friend to {c.Name}*\n\nForward this message to them:\n\n" +
               $"Join me on {c.Name} on AlumUnion: {link}\n\n" +
               "When they join using your link it counts toward your referrals.";
    }

    private static void ClearFlow(AssistantState state)
    {
        state.Flow = null;
        state.FlowStep = 0;
        state.FlowData = [];
    }

    private string StartFlow(string flow, AssistantState state)
    {
        ClearFlow(state);
        state.Flow = flow;
        state.Stage = AssistantStages.Menu;
        return flow == "suggest"
            ? "Let's suggest an opportunity. An administrator reviews it before anyone sees it. Reply *cancel* at any time to stop.\n\nWhat is it called? (for example *Graduate Trainee, Finance*)"
            : $"Write your update (up to {MaxNoteLength} characters). It will show on your year group's wall. I'll show it to you before it is posted. Reply *cancel* to stop.";
    }

    private async Task<string> ContinueFlowAsync(string text, string raw, AssistantState state, AssistantCommunity c, CancellationToken ct)
    {
        if (state.Flow == "note")
        {
            if (state.FlowStep == 0)
            {
                if (raw.Length < 3) return "That's very short. Write your update, or reply *cancel*.";
                if (raw.Length > MaxNoteLength) return $"That's {raw.Length} characters; the limit is {MaxNoteLength}. Please shorten it, or reply *cancel*.";
                state.FlowData["content"] = raw;
                state.FlowStep = 1;
                return $"This will be posted to your year group's wall:\n\n\"{raw}\"\n\nReply *yes* to post it, or *cancel*.";
            }
            if (text is "yes" or "y" or "post" or "ok" or "confirm")
            {
                var content = state.FlowData.GetValueOrDefault("content") ?? "";
                ClearFlow(state);
                var res = await actions.PostUpdateAsync(c, content, ct);
                return res.Message;
            }
            return "Reply *yes* to post it, or *cancel* to drop it.";
        }

        // suggest an opportunity: title, company, location, type, link, confirm
        var d = state.FlowData;
        switch (state.FlowStep)
        {
            case 0:
                if (raw.Length is < 3 or > 120) return "Give it a title of 3 to 120 characters, or reply *cancel*.";
                d["title"] = raw; state.FlowStep = 1;
                return "Who is offering it? (company or organisation)";
            case 1:
                if (raw.Length is < 2 or > 120) return "Please say who is offering it (2 to 120 characters), or reply *cancel*.";
                d["company"] = raw; state.FlowStep = 2;
                return "Where is it? (city or *remote*), or reply *skip*.";
            case 2:
                d["location"] = text == "skip" ? "" : raw.Length > 120 ? raw[..120] : raw;
                state.FlowStep = 3;
                var types = actions.OpportunityTypes;
                var list = string.Join("\n", types.Select((t, i) => $"{i + 1}. {t}"));
                return $"What kind is it?\n{list}\n\nReply with a number.";
            case 3:
                var kinds = actions.OpportunityTypes;
                if (!int.TryParse(text, out var k) || k < 1 || k > kinds.Count) return $"Reply with a number from 1 to {kinds.Count}, or *cancel*.";
                d["type"] = kinds[k - 1]; state.FlowStep = 4;
                return "Is there a link to apply or read more? Send it, or reply *skip*.";
            case 4:
                if (text != "skip")
                {
                    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                        return "That doesn't look like a web link. It should start with *https://*. Send it again, or reply *skip*.";
                    d["url"] = uri.ToString();
                }
                state.FlowStep = 5;
                return $"Please check:\n*{d["title"]}*\n{d["company"]}{(string.IsNullOrWhiteSpace(d.GetValueOrDefault("location")) ? "" : $" · {d["location"]}")}\nType: {d["type"]}{(d.TryGetValue("url", out var u) ? $"\n{u}" : "")}\n\nReply *yes* to send it for review, or *cancel*.";
            default:
                if (text is "yes" or "y" or "send" or "ok" or "confirm")
                {
                    var draft = new OpportunityDraft(d["title"], d["company"], d.GetValueOrDefault("location") ?? "", d["type"], d.GetValueOrDefault("url"));
                    ClearFlow(state);
                    var res = await actions.SuggestOpportunityAsync(c, draft, ct);
                    return res.Message;
                }
                return "Reply *yes* to send it for review, or *cancel* to drop it.";
        }
    }

    private const string GroupCommandInPrivate =
        "Commands that start with ! are for groups. To connect a WhatsApp *group*, add me to the group and post the *!link CODE* message there, " +
        "not here.\n\nTo connect your own account, open the *Connect WhatsApp* page in your portal and send me the *LINK* message it gives you. ";

    private static string MediaReply(string kind) => kind switch
    {
        "voice" => "I can't listen to voice notes yet. Please type your message.",
        "image" or "video" => "I can't open pictures or videos here. Please type your message.",
        "document" => "I can't open files here. Please type your message.",
        "location" => "Thanks, but I can't use locations. Please type your message.",
        "contact" => "I can't use contact cards. Please type your message.",
        "sticker" => "Say *hi* any time and I'll show you what I can do.",
        _ => "I can only read text messages. Please type your message.",
    };

    // ── Helpers ──────────────────────────────────────────────────────────

    private static readonly Regex ChoiceWords = new(
        @"^(?:option|choice|number|no|num|item)?\.?\s*#?\s*(\d{1,2}|one|two|three|four|five|six|seven|eight|nine)\s*[).:\-]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] NumberWords = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>
    /// Lower-cased, trimmed text, with the ways people answer a numbered menu folded to the digit: "1", "1.", "1)", "*1*", "1️⃣",
    /// "option 2", "No. 3", "two". Anything else is left as typed.
    /// </summary>
    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        // Keycap emoji are the digit followed by U+FE0F and U+20E3; WhatsApp also lets people bold with *…*.
        var t = text.Replace("\uFE0F", "").Replace("\u20E3", "").Replace("*", "").Replace("_", "").Trim().Trim('.', '!', ',', '"', '\'').ToLowerInvariant();
        var m = ChoiceWords.Match(t);
        if (!m.Success) return t;
        var v = m.Groups[1].Value;
        var word = Array.IndexOf(NumberWords, v);
        return word >= 0 ? word.ToString() : v;
    }

}
