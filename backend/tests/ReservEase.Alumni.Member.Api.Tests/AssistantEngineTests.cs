using Microsoft.Extensions.Options;
using ReservEase.Alumni.Member.Api.Services.Assistant;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;

namespace ReservEase.Alumni.Member.Api.Tests;

public class AssistantEngineTests
{
    private const string Phone = "233244123456";

    private static AssistantCommunity Umat(bool member = false, bool verified = false) =>
        new("inst-umat", "umat", "UMaT Alumni", "https://umat.example.org", member ? "m-1" : null, verified, member ? "Ama" : null);

    private static AssistantCommunity Pensa(bool member = false, bool verified = false) =>
        new("inst-pensa", "pensa", "PENSA UMaT", "https://pensa.example.org", member ? "m-2" : null, verified, member ? "Ama" : null);

    private sealed class FakeDirectory(IReadOnlyList<AssistantCommunity> mine, params AssistantCommunity[] everyone) : IAssistantDirectory
    {
        public Task<IReadOnlyList<AssistantCommunity>> FindMemberCommunitiesAsync(string phone, CancellationToken ct = default)
            => Task.FromResult(phone == Phone ? mine : (IReadOnlyList<AssistantCommunity>)[]);
        public Task<AssistantCommunity?> FindByIdAsync(string id, CancellationToken ct = default) => Task.FromResult(Plain(everyone.FirstOrDefault(c => c.InstitutionId == id)));
        public Task<AssistantCommunity?> FindBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Plain(everyone.FirstOrDefault(c => c.Slug == slug)));
        public Task<IReadOnlyList<AssistantCommunity>> SearchAsync(string text, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AssistantCommunity>>(everyone.Where(c => c.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).Select(c => Plain(c)!).ToList());
        // The public directory knows nothing about who is asking.
        private static AssistantCommunity? Plain(AssistantCommunity? c) => c is null ? null : c with { MemberId = null, Verified = false, MemberFirstName = null };
    }

    private sealed class FakeContent : IAssistantContent
    {
        public List<string> Calls { get; } = [];
        public HashSet<string> Disabled { get; } = [];
        public AssistantDues? Dues { get; set; } = new(CurrentYearPaid: false, HasArrears: false, UnpaidCount: 1, "https://umat.example.org/contributions");

        private Task<IReadOnlyList<AssistantItem>> Items(string call, string title)
        {
            Calls.Add(call);
            return Task.FromResult<IReadOnlyList<AssistantItem>>([new(title, "detail", $"https://umat.example.org/{call}/1")]);
        }
        public Task<IReadOnlyList<AssistantItem>> PublicCampaignsAsync(AssistantCommunity c, CancellationToken ct = default) => Items("public-campaigns", "Library fund");
        public Task<IReadOnlyList<AssistantItem>> PublicEventsAsync(AssistantCommunity c, CancellationToken ct = default) => Items("public-events", "Open day");
        public Task<IReadOnlyList<AssistantItem>> PublicJobsAsync(AssistantCommunity c, CancellationToken ct = default) => Items("public-jobs", "Open role");
        public Task<IReadOnlyList<AssistantItem>> PublicUpdatesAsync(AssistantCommunity c, CancellationToken ct = default) => Items("public-news", "Open news");
        public Task<IReadOnlyList<AssistantItem>> MemberCampaignsAsync(AssistantCommunity c, CancellationToken ct = default) => Items("member-campaigns", "Alumni lab fund");
        public Task<IReadOnlyList<AssistantItem>> MemberEventsAsync(AssistantCommunity c, CancellationToken ct = default)
        {
            Calls.Add("member-events");
            return Task.FromResult<IReadOnlyList<AssistantItem>>([new("Class of 2015 dinner", "detail", "https://umat.example.org/events/ev-1", "ev-1"), new("Career mixer", "detail", "https://umat.example.org/events/ev-2", "ev-2")]);
        }
        public Task<IReadOnlyList<AssistantItem>> MemberJobsAsync(AssistantCommunity c, CancellationToken ct = default) => Items("member-jobs", "Member role");
        public Task<IReadOnlyList<AssistantItem>> MemberUpdatesAsync(AssistantCommunity c, CancellationToken ct = default) => Items("member-news", "Member news");
        public Task<AssistantDues?> MemberDuesAsync(AssistantCommunity c, CancellationToken ct = default) { Calls.Add("member-dues"); return Task.FromResult(Dues); }
        public Task<bool> IsAvailableAsync(AssistantCommunity c, string feature, CancellationToken ct = default) => Task.FromResult(!Disabled.Contains(feature));
    }

    private sealed class MemoryState : IAssistantStateStore
    {
        public Dictionary<string, AssistantState> Store { get; } = [];
        public Task<AssistantState?> GetAsync(string phone) => Task.FromResult(Store.GetValueOrDefault(phone));
        public Task SetAsync(string phone, AssistantState state) { Store[phone] = state; return Task.CompletedTask; }
    }

    private sealed class FakeLinker : IAssistantLinker
    {
        public LinkOutcome Outcome { get; set; } = LinkOutcome.Invalid;
        public List<(string Code, string Phone)> Completed { get; } = [];
        public int Unlinked { get; set; }
        public int UnlinkResult { get; set; }
        public Task<LinkOutcome> CompleteAsync(string code, string senderPhone, CancellationToken ct = default) { Completed.Add((code, senderPhone)); return Task.FromResult(Outcome); }
        public Task<int> UnlinkAsync(string senderPhone, CancellationToken ct = default) { Unlinked++; return Task.FromResult(UnlinkResult); }
        public List<bool> AlertChanges { get; } = [];
        public int AlertsResult { get; set; } = 1;
        public Task<int> SetAlertsAsync(string senderPhone, bool on, CancellationToken ct = default) { AlertChanges.Add(on); return Task.FromResult(AlertsResult); }
    }

    private sealed class FakeActions : IAssistantActions
    {
        public IReadOnlyList<string> OpportunityTypes { get; } = ["Full-time", "Internship", "Other"];
        public List<string> Rsvps { get; } = [];
        public List<string> Cancels { get; } = [];
        public List<OpportunityDraft> Suggested { get; } = [];
        public List<string> Posted { get; } = [];
        public Task<ActionResult> RsvpAsync(AssistantCommunity c, string eventId, CancellationToken ct = default) { Rsvps.Add(eventId); return Task.FromResult(new ActionResult(true, "You're booked in.")); }
        public Task<ActionResult> CancelRsvpAsync(AssistantCommunity c, string eventId, CancellationToken ct = default) { Cancels.Add(eventId); return Task.FromResult(new ActionResult(true, "Cancelled.")); }
        public Task<ActionResult> SuggestOpportunityAsync(AssistantCommunity c, OpportunityDraft d, CancellationToken ct = default) { Suggested.Add(d); return Task.FromResult(new ActionResult(true, "Sent for review.")); }
        public Task<ActionResult> PostUpdateAsync(AssistantCommunity c, string content, CancellationToken ct = default) { Posted.Add(content); return Task.FromResult(new ActionResult(true, "Posted.")); }
    }

    private static FakeLinker LastLinker = new();
    private static FakeActions LastActions = new();

    private static (AssistantEngine engine, FakeContent content, MemoryState state) Create(IReadOnlyList<AssistantCommunity>? mine = null, params AssistantCommunity[] everyone)
    {
        var content = new FakeContent();
        var state = new MemoryState();
        var all = everyone.Length > 0 ? everyone : [Umat(), Pensa()];
        LastLinker = new FakeLinker();
        LastActions = new FakeActions();
        var engine = new AssistantEngine(new FakeDirectory(mine ?? [], all), content, state, LastLinker, LastActions,
            Options.Create(new WaSenderConfig { Enabled = true, AssistantNumber = "233200000000" }));
        return (engine, content, state);
    }

    private static WhatsAppInboundEvent Say(string text, string? phone = Phone)
        => new(WhatsAppInboundKind.PersonalMessage, Guid.NewGuid().ToString("N"), phone, null, phone, text, DateTimeOffset.UtcNow);

    // ── Guests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_stranger_is_asked_which_community_and_a_name_gets_them_the_guest_menu()
    {
        var (engine, content, _) = Create();

        var first = await engine.HandleAsync(Say("Hi"));
        Assert.Contains("Which community", first!.Text);
        Assert.Equal(Phone, first.To);

        var second = await engine.HandleAsync(Say("umat alumni"));
        Assert.Contains("UMaT Alumni", second!.Text);
        Assert.Contains("browsing as a guest", second.Text);
        Assert.Empty(content.Calls);
    }

    [Fact]
    public async Task A_community_link_with_a_start_code_goes_straight_to_that_community()
    {
        var (engine, _, _) = Create();
        var reply = await engine.HandleAsync(Say("START pensa"));
        Assert.Contains("PENSA UMaT", reply!.Text);
    }

    [Fact]
    public async Task A_guest_sees_only_public_information_and_is_pointed_to_linking_for_anything_personal()
    {
        var (engine, content, _) = Create();
        await engine.HandleAsync(Say("start umat"));

        var campaigns = await engine.HandleAsync(Say("1"));
        Assert.Contains("Library fund", campaigns!.Text);
        Assert.Contains("link", campaigns.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("member-dues", content.Calls);

        await engine.HandleAsync(Say("menu"));
        var events = await engine.HandleAsync(Say("2"));
        Assert.Contains("https://umat.example.org/public-events/1", events!.Text);

        await engine.HandleAsync(Say("menu"));
        var mine = await engine.HandleAsync(Say("5"));
        Assert.Contains("browsing as a guest", mine!.Text);
        Assert.DoesNotContain(content.Calls, c => c.StartsWith("member-"));
    }

    [Fact]
    public async Task A_number_matched_but_not_verified_is_still_a_guest()
    {
        var (engine, content, _) = Create([Umat(member: true, verified: false)]);

        var hello = await engine.HandleAsync(Say("hi"));
        Assert.Contains("guest", hello!.Text);

        await engine.HandleAsync(Say("1"));
        Assert.DoesNotContain(content.Calls, c => c.StartsWith("member-"));
    }

    // ── Verified members ─────────────────────────────────────────────────

    [Fact]
    public async Task A_verified_member_with_one_community_goes_straight_to_a_personal_menu()
    {
        var (engine, content, _) = Create([Umat(member: true, verified: true)]);

        var hello = await engine.HandleAsync(Say("hello"));
        Assert.Contains("Welcome back, Ama", hello!.Text);

        var dues = await engine.HandleAsync(Say("1"));
        Assert.Contains("not paid yet", dues!.Text);
        Assert.Contains("https://umat.example.org/contributions", dues.Text);
        Assert.Contains("member-dues", content.Calls);
        Assert.Contains("Alumni lab fund", dues.Text);

        var events = await engine.HandleAsync(Say("2"));
        Assert.Contains("Class of 2015 dinner", events!.Text);
    }

    [Fact]
    public async Task Paid_up_dues_are_reported_plainly()
    {
        var (engine, content, _) = Create([Umat(member: true, verified: true)]);
        content.Dues = new AssistantDues(CurrentYearPaid: true, HasArrears: false, UnpaidCount: 0, "https://umat.example.org/contributions");
        await engine.HandleAsync(Say("hi"));

        var dues = await engine.HandleAsync(Say("1"));
        Assert.Contains("up to date", dues!.Text);
        Assert.DoesNotContain("Pay here", dues.Text);
    }

    [Fact]
    public async Task Someone_in_two_communities_chooses_and_can_switch()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true), Pensa(member: true, verified: true)]);

        var ask = await engine.HandleAsync(Say("hi"));
        Assert.Contains("1. UMaT Alumni", ask!.Text);
        Assert.Contains("2. PENSA UMaT", ask.Text);

        var picked = await engine.HandleAsync(Say("2"));
        Assert.Contains("PENSA UMaT", picked!.Text);
        Assert.Contains("Welcome back", picked.Text);

        var switching = await engine.HandleAsync(Say("6"));
        Assert.Contains("Which community", switching!.Text);

        var other = await engine.HandleAsync(Say("1"));
        Assert.Contains("UMaT Alumni", other!.Text);
    }

    // ── Switched off, noise, limits ──────────────────────────────────────

    [Fact]
    public async Task A_community_that_switched_the_assistant_off_says_so_and_points_to_the_portal()
    {
        var (engine, content, _) = Create();
        content.Disabled.Add(InstitutionFeatures.WhatsApp);

        var reply = await engine.HandleAsync(Say("start umat"));
        Assert.Contains("hasn't turned on", reply!.Text);
        Assert.Contains("https://umat.example.org", reply.Text);
    }

    [Fact]
    public async Task A_disabled_part_of_the_product_is_not_shown()
    {
        var (engine, content, _) = Create();
        content.Disabled.Add(InstitutionFeatures.Jobs);
        await engine.HandleAsync(Say("start umat"));

        var jobs = await engine.HandleAsync(Say("3"));
        Assert.Contains("isn't available", jobs!.Text);
    }

    [Fact]
    public async Task Our_own_messages_and_group_messages_get_no_assistant_reply()
    {
        var (engine, _, _) = Create();
        Assert.Null(await engine.HandleAsync(Say("hi") with { FromMe = true }));
        Assert.Null(await engine.HandleAsync(Say("hi") with { Kind = WhatsAppInboundKind.GroupMessage }));
    }

    [Fact]
    public async Task A_sender_with_no_readable_number_gets_a_plain_explanation()
    {
        var (engine, _, _) = Create();
        var reply = await engine.HandleAsync(Say("hi", phone: null) with { ChatId = "555@lid" });
        Assert.Contains("couldn't read your WhatsApp number", reply!.Text);
        Assert.Equal("555@lid", reply.To);
    }

    [Fact]
    public async Task Messages_are_never_rate_limited_so_a_quick_conversation_always_gets_every_answer()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        for (var i = 0; i < 60; i++)
        {
            var r = await engine.HandleAsync(Say("menu"));
            Assert.NotNull(r);
            Assert.Contains("Reply with a number", r!.Text);
        }
    }

    [Fact]
    public async Task Text_that_means_nothing_is_met_with_the_menu_not_an_error()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        await engine.HandleAsync(Say("hi"));
        var reply = await engine.HandleAsync(Say("qwerty asdf"));
        Assert.Contains("didn't understand", reply!.Text);
        Assert.Contains("1. Contributions", reply.Text);
    }

    // ── Linking by message ───────────────────────────────────────────────

    [Fact]
    public async Task A_valid_code_connects_the_account_and_the_chat_continues_in_that_community()
    {
        var (engine, _, state) = Create();
        LastLinker.Outcome = new LinkOutcome(LinkStatus.Linked, "UMaT Alumni", "Ama", "inst-umat", AlsoLinked: 1);

        var reply = await engine.HandleAsync(Say("LINK K7M2QX9P"));

        Assert.Contains("You're connected, Ama", reply!.Text);
        Assert.Contains("same email are connected too", reply.Text);
        Assert.Equal(("K7M2QX9P".ToLowerInvariant(), Phone), (LastLinker.Completed.Single().Code, LastLinker.Completed.Single().Phone));
        Assert.Equal("inst-umat", state.Store[Phone].InstitutionId);
    }

    [Fact]
    public async Task The_number_used_is_the_providers_sender_never_something_in_the_message()
    {
        var (engine, _, _) = Create();
        LastLinker.Outcome = new LinkOutcome(LinkStatus.Linked, "UMaT Alumni", null, "inst-umat");

        await engine.HandleAsync(Say("link K7M2QX9P 233500000000"));   // extra text is not a valid command
        Assert.Empty(LastLinker.Completed);

        await engine.HandleAsync(Say("link K7M2QX9P"));
        Assert.Equal(Phone, LastLinker.Completed.Single().Phone);
    }

    [Theory]
    [InlineData(LinkStatus.Conflict, "already connected to a different account")]
    [InlineData(LinkStatus.NotAllowed, "can't be connected right now")]
    [InlineData(LinkStatus.InvalidCode, "didn't work")]
    public async Task A_refused_link_says_why_without_revealing_whose_account_it_was(LinkStatus status, string expected)
    {
        var (engine, _, _) = Create();
        LastLinker.Outcome = new LinkOutcome(status);
        var reply = await engine.HandleAsync(Say("link K7M2QX9P"));
        Assert.Contains(expected, reply!.Text);
    }

    [Fact]
    public async Task Repeated_wrong_codes_pause_linking_for_that_sender()
    {
        var (engine, _, _) = Create();
        for (var i = 0; i < 5; i++) await engine.HandleAsync(Say($"link BADCODE{i + 2}"));

        var blocked = await engine.HandleAsync(Say("link K7M2QX9P"));

        Assert.Contains("Too many wrong codes", blocked!.Text);
        Assert.Equal(5, LastLinker.Completed.Count);   // the sixth never reached the service
    }

    [Fact]
    public async Task Unlinking_disconnects_every_account_on_the_number_and_confirms()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        LastLinker.UnlinkResult = 2;

        var reply = await engine.HandleAsync(Say("unlink"));

        Assert.Equal(1, LastLinker.Unlinked);
        Assert.Contains("no longer connected (2 accounts)", reply!.Text);
    }

    [Fact]
    public async Task Unlinking_when_nothing_is_connected_says_so()
    {
        var (engine, _, _) = Create();
        var reply = await engine.HandleAsync(Say("unlink"));
        Assert.Contains("isn't connected", reply!.Text);
    }

    // ── Opting out, other message kinds, doing things ────────────────────

    private static WhatsAppInboundEvent Media(string kind, string? phone = Phone)
        => new(WhatsAppInboundKind.PersonalMessage, Guid.NewGuid().ToString("N"), phone, null, phone, null, DateTimeOffset.UtcNow, MediaKind: kind);

    [Theory]
    [InlineData("STOP")]
    [InlineData("stop.")]
    [InlineData("Unsubscribe")]
    public async Task Stop_switches_alerts_off_without_disconnecting(string word)
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);

        var reply = await engine.HandleAsync(Say(word));

        Assert.Equal([false], LastLinker.AlertChanges);
        Assert.Equal(0, LastLinker.Unlinked);
        Assert.Contains("stopped alerts", reply!.Text);
    }

    [Fact]
    public async Task Stop_from_a_number_that_gets_no_alerts_says_so()
    {
        var (engine, _, _) = Create();
        LastLinker.AlertsResult = 0;

        var reply = await engine.HandleAsync(Say("stop"));

        Assert.Contains("No alerts are being sent", reply!.Text);
    }

    [Fact]
    public async Task Alerts_on_turns_them_back_on_and_start_is_still_a_greeting()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);

        var on = await engine.HandleAsync(Say("alerts on"));
        Assert.Equal([true], LastLinker.AlertChanges);
        Assert.Contains("again", on!.Text);

        var start = await engine.HandleAsync(Say("start"));
        Assert.Single(LastLinker.AlertChanges);
        Assert.Contains("Welcome back", start!.Text);
    }

    [Theory]
    [InlineData("voice", "voice notes")]
    [InlineData("image", "pictures")]
    [InlineData("location", "locations")]
    public async Task A_message_with_no_text_gets_a_specific_answer(string kind, string expected)
    {
        var (engine, _, _) = Create();

        var reply = await engine.HandleAsync(Media(kind));

        Assert.Contains(expected, reply!.Text);
        Assert.DoesNotContain("didn't understand", reply.Text);
    }

    [Fact]
    public async Task A_member_can_rsvp_to_an_event_from_the_list_they_were_shown()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        await engine.HandleAsync(Say("hi"));

        var list = await engine.HandleAsync(Say("2"));
        Assert.Contains("rsvp 1", list!.Text);

        var booked = await engine.HandleAsync(Say("RSVP 2"));
        Assert.Equal(["ev-2"], LastActions.Rsvps);
        Assert.Contains("booked in", booked!.Text);

        await engine.HandleAsync(Say("cancel rsvp 2"));
        Assert.Equal(["ev-2"], LastActions.Cancels);
    }

    [Fact]
    public async Task Rsvp_without_having_seen_the_list_or_for_a_number_not_on_it_does_nothing()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        await engine.HandleAsync(Say("hi"));

        var blind = await engine.HandleAsync(Say("rsvp 1"));
        Assert.Empty(LastActions.Rsvps);
        Assert.Contains("see the events first", blind!.Text);

        await engine.HandleAsync(Say("2"));
        var outOfRange = await engine.HandleAsync(Say("rsvp 9"));
        Assert.Empty(LastActions.Rsvps);
        Assert.Contains("see the events first", outOfRange!.Text);
    }

    [Fact]
    public async Task A_guest_cannot_rsvp_or_start_a_flow()
    {
        var (engine, _, _) = Create();
        await engine.HandleAsync(Say("start umat"));

        var rsvp = await engine.HandleAsync(Say("rsvp 1"));
        Assert.Contains("link", rsvp!.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(LastActions.Rsvps);

        await engine.HandleAsync(Say("suggest"));
        Assert.Empty(LastActions.Suggested);
    }

    [Fact]
    public async Task A_member_suggests_an_opportunity_step_by_step_and_nothing_is_sent_until_they_confirm()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        await engine.HandleAsync(Say("hi"));

        Assert.Contains("What is it called", (await engine.HandleAsync(Say("suggest")))!.Text);
        await engine.HandleAsync(Say("Graduate Trainee, Finance"));
        await engine.HandleAsync(Say("Acme Bank"));
        await engine.HandleAsync(Say("Accra"));
        await engine.HandleAsync(Say("2"));
        var check = await engine.HandleAsync(Say("https://acme.example/jobs/1"));
        Assert.Contains("Please check", check!.Text);
        Assert.Empty(LastActions.Suggested);

        var done = await engine.HandleAsync(Say("yes"));
        var sent = Assert.Single(LastActions.Suggested);
        Assert.Equal(("Graduate Trainee, Finance", "Acme Bank", "Accra", "Internship", "https://acme.example/jobs/1"),
            (sent.Title, sent.Company, sent.Location, sent.Type, sent.ApplyUrl));
        Assert.Contains("Sent for review", done!.Text);
    }

    [Fact]
    public async Task Typing_cancel_inside_a_flow_sends_nothing()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        await engine.HandleAsync(Say("hi"));
        await engine.HandleAsync(Say("8"));
        await engine.HandleAsync(Say("2"));
        await engine.HandleAsync(Say("Our reunion is on Saturday"));

        var reply = await engine.HandleAsync(Say("cancel"));

        Assert.Empty(LastActions.Posted);
        Assert.Contains("Nothing was sent", reply!.Text);
    }

    [Fact]
    public async Task A_bad_link_is_refused_and_a_yes_posts_the_update_once()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        await engine.HandleAsync(Say("hi"));
        await engine.HandleAsync(Say("post update"));
        await engine.HandleAsync(Say("Reunion this Saturday at 4pm"));

        await engine.HandleAsync(Say("yes"));
        var again = await engine.HandleAsync(Say("yes"));

        Assert.Single(LastActions.Posted);
        Assert.Equal("Reunion this Saturday at 4pm", LastActions.Posted[0]);   // case kept: it is the member's own words
        Assert.Contains("didn't understand", again!.Text);
    }

    [Fact]
    public async Task A_redelivered_message_is_not_handled_twice()
    {
        var (engine, _, _) = Create([Umat(member: true, verified: true)]);
        await engine.HandleAsync(Say("hi"));
        await engine.HandleAsync(Say("post update"));
        await engine.HandleAsync(Say("Reunion this Saturday at 4pm"));
        var yes = new WhatsAppInboundEvent(WhatsAppInboundKind.PersonalMessage, "same-id", Phone, null, Phone, "yes", DateTimeOffset.UtcNow);

        var first = await engine.HandleAsync(yes);
        var second = await engine.HandleAsync(yes);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Single(LastActions.Posted);
    }

    [Fact]
    public async Task The_share_option_only_exists_for_a_connected_member()
    {
        var (guestEngine, _, _) = Create();
        await guestEngine.HandleAsync(Say("start umat"));
        Assert.DoesNotContain("8.", (await guestEngine.HandleAsync(Say("menu")))!.Text);

        var (memberEngine, _, _) = Create([Umat(member: true, verified: true)]);
        Assert.Contains("8. Share", (await memberEngine.HandleAsync(Say("menu")))!.Text);
    }
}
