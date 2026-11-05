using ReservEase.Alumni.Member.Api.Services.Realtime;

namespace ReservEase.Alumni.Member.Api.Tests;

public class IntroductionEventsTests
{
    private static List<IntroductionEvent> Drain(IntroductionSubscription s)
    {
        var list = new List<IntroductionEvent>();
        while (s.Reader.TryRead(out var e)) list.Add(e);
        return list;
    }

    [Fact]
    public void A_notice_reaches_only_the_member_it_is_for_in_that_institution()
    {
        var hub = new InMemoryIntroductionEvents();
        using var ama = hub.Subscribe("inst-1", "ama");
        using var kofi = hub.Subscribe("inst-1", "kofi");
        using var sameIdElsewhere = hub.Subscribe("inst-2", "ama");
        hub.Publish("inst-1", "ama", new("message", "i1"));
        Assert.Equal("message", Assert.Single(Drain(ama)).Kind);
        Assert.Empty(Drain(kofi));
        Assert.Empty(Drain(sameIdElsewhere));
    }

    [Fact]
    public void Every_open_tab_of_a_member_hears_it_and_a_closed_tab_stops()
    {
        var hub = new InMemoryIntroductionEvents();
        var one = hub.Subscribe("i", "ama");
        using var two = hub.Subscribe("i", "ama");
        hub.Publish("i", "ama", new("request", "x"));
        Assert.Single(Drain(one)); Assert.Single(Drain(two));
        one.Dispose();
        hub.Publish("i", "ama", new("request", "y"));
        Assert.Single(Drain(two));
        Assert.Equal(1, hub.OpenConnections);
        two.Dispose();
        Assert.Equal(0, hub.OpenConnections);   // nothing is left behind
    }

    [Fact]
    public void A_sixth_tab_closes_the_oldest_instead_of_growing_without_limit()
    {
        var hub = new InMemoryIntroductionEvents();
        var subs = Enumerable.Range(0, InMemoryIntroductionEvents.MaxPerMember + 1).Select(_ => hub.Subscribe("i", "ama")).ToList();
        Assert.Equal(InMemoryIntroductionEvents.MaxPerMember, hub.OpenConnections);
        Assert.True(subs[0].Reader.Completion.IsCompleted);          // the oldest was closed
        Assert.False(subs[^1].Reader.Completion.IsCompleted);
        subs.ForEach(s => s.Dispose());
    }

    [Fact]
    public void A_flood_never_blocks_the_sender_and_keeps_the_newest()
    {
        var hub = new InMemoryIntroductionEvents();
        using var slow = hub.Subscribe("i", "ama");               // never reads
        for (var n = 0; n < 1000; n++) hub.Publish("i", "ama", new("message", $"i{n}"));
        var kept = Drain(slow);
        Assert.True(kept.Count <= 64);
        Assert.Equal("i999", kept[^1].IntroductionId);
    }

    [Fact]
    public void Publishing_to_someone_who_is_not_connected_is_harmless()
    {
        new InMemoryIntroductionEvents().Publish("i", "nobody", new("message", "x"));
    }

    // ── What the service announces, and to whom ──────────────────────────────────────

    private static (IntroductionServiceTests.RigAccess Rig, IntroductionSubscription Ama, IntroductionSubscription Kofi) Listening()
    {
        var rig = new IntroductionServiceTests.RigAccess();
        return (rig, rig.Events.Subscribe("inst-1", "ama"), rig.Events.Subscribe("inst-1", "kofi"));
    }

    [Fact]
    public async Task Asking_tells_the_other_person_and_accepting_tells_the_asker()
    {
        var (rig, ama, kofi) = Listening();
        var id = (await rig.Service.RequestAsync(new("kofi", "Same programme, moving into data work."), rig.Ama)).Data!.IntroductionId!;
        Assert.Equal("request", Assert.Single(Drain(kofi)).Kind);
        Assert.Empty(Drain(ama));

        await rig.Service.RespondAsync(id, true, rig.Kofi);
        Assert.Equal("accepted", Assert.Single(Drain(ama)).Kind);
        Assert.Equal("changed", Assert.Single(Drain(kofi)).Kind);   // his own other tabs
    }

    [Fact]
    public async Task A_decline_is_never_announced_to_the_person_who_asked()
    {
        var (rig, ama, kofi) = Listening();
        var id = (await rig.Service.RequestAsync(new("kofi", "Same programme, moving into data work."), rig.Ama)).Data!.IntroductionId!;
        Drain(kofi);
        await rig.Service.RespondAsync(id, false, rig.Kofi);
        Assert.Empty(Drain(ama));
    }

    [Fact]
    public async Task A_message_is_announced_to_both_and_reading_it_tells_the_sender_it_was_seen()
    {
        var (rig, ama, kofi) = Listening();
        var id = await rig.ConnectedAsync();
        Drain(ama); Drain(kofi);

        await rig.Service.SendMessageAsync(id, "hello", rig.Ama);
        Assert.Equal("message", Assert.Single(Drain(kofi)).Kind);
        Assert.Equal("message", Assert.Single(Drain(ama)).Kind);

        await rig.Service.GetAsync(id, null, rig.Kofi);
        Assert.Contains(Drain(ama), e => e.Kind == "read");
        await rig.Service.GetAsync(id, null, rig.Kofi);     // nothing new to mark: no further notice
        Assert.Empty(Drain(ama));
    }

    [Fact]
    public async Task Ending_and_blocking_refresh_both_sides_without_any_text()
    {
        var (rig, ama, kofi) = Listening();
        var id = await rig.ConnectedAsync();
        Drain(ama); Drain(kofi);
        await rig.Service.EndAsync(id, rig.Kofi);
        Assert.Equal("changed", Assert.Single(Drain(ama)).Kind);       // the conversation stays, so the page just refreshes
        Drain(kofi);
        await rig.Service.BlockAsync("ama", rig.Kofi);
        Assert.All(Drain(ama).Concat(Drain(kofi)), e => Assert.Equal(("changed", id), (e.Kind, e.IntroductionId)));
        await rig.Service.UnblockAsync("ama", rig.Kofi);
        Assert.Equal(2, Drain(ama).Concat(Drain(kofi)).Count());       // both sides refresh when it is undone
    }
}
