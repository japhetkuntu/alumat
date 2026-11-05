using ReservEase.Alumni.Member.Api.Services.Realtime;

namespace ReservEase.Alumni.Member.Api.Tests;

public class IntroductionRelayTests
{
    /// <summary>A stand-in for Redis: what one server sends, every OTHER server receives.</summary>
    private sealed class Bus
    {
        private readonly List<FakeRelay> _servers = new();
        public FakeRelay Join() { var r = new FakeRelay(this); _servers.Add(r); return r; }
        public void Deliver(FakeRelay from, string payload) { foreach (var s in _servers.Where(s => s != from)) s.Raise(payload); }
    }
    private sealed class FakeRelay(Bus bus) : IEventRelay
    {
        public event Action<string>? Received;
        public void Send(string payload) => bus.Deliver(this, payload);
        public void Raise(string payload) => Received?.Invoke(payload);
    }
    private sealed class BrokenRelay : IEventRelay
    {
        public event Action<string>? Received { add { } remove { } }
        public void Send(string payload) => throw new InvalidOperationException("Redis is down");
    }

    private static List<IntroductionEvent> Drain(IntroductionSubscription s) { var l = new List<IntroductionEvent>(); while (s.Reader.TryRead(out var e)) l.Add(e); return l; }

    [Fact]
    public void A_member_on_one_server_hears_what_happened_on_another_exactly_once()
    {
        var bus = new Bus();
        var serverA = new RelayedIntroductionEvents(new InMemoryIntroductionEvents(), bus.Join());
        var serverB = new RelayedIntroductionEvents(new InMemoryIntroductionEvents(), bus.Join());
        using var kofiOnB = serverB.Subscribe("inst", "kofi");
        using var amaOnA = serverA.Subscribe("inst", "ama");

        serverA.Publish("inst", "kofi", new("message", "i1"));          // Ama sends from server A, Kofi is connected to server B
        var heard = Assert.Single(Drain(kofiOnB));
        Assert.Equal(("message", "i1"), (heard.Kind, heard.IntroductionId));
        Assert.Empty(Drain(amaOnA));                                    // nothing leaks to someone else

        serverB.Publish("inst", "ama", new("read", "i1"));
        Assert.Single(Drain(amaOnA));
        Assert.Empty(Drain(kofiOnB));
    }

    [Fact]
    public void A_server_does_not_deliver_its_own_notice_twice()
    {
        var bus = new Bus();
        var serverA = new RelayedIntroductionEvents(new InMemoryIntroductionEvents(), bus.Join());
        bus.Join();   // another server that is listening
        using var sub = serverA.Subscribe("inst", "ama");
        serverA.Publish("inst", "ama", new("message", "i1"));
        Assert.Single(Drain(sub));
    }

    [Fact]
    public void Members_of_another_institution_with_the_same_id_hear_nothing()
    {
        var bus = new Bus();
        var a = new RelayedIntroductionEvents(new InMemoryIntroductionEvents(), bus.Join());
        var b = new RelayedIntroductionEvents(new InMemoryIntroductionEvents(), bus.Join());
        using var other = b.Subscribe("inst-2", "ama");
        a.Publish("inst-1", "ama", new("message", "i1"));
        Assert.Empty(Drain(other));
    }

    [Fact]
    public void A_down_relay_never_stops_local_delivery()
    {
        var server = new RelayedIntroductionEvents(new InMemoryIntroductionEvents(), new BrokenRelay());
        using var sub = server.Subscribe("inst", "ama");
        server.Publish("inst", "ama", new("message", "i1"));
        Assert.Single(Drain(sub));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"I\":\"inst\"}")]
    public void Garbage_arriving_from_the_relay_is_ignored(string payload)
    {
        var bus = new Bus();
        var relay = bus.Join();
        var server = new RelayedIntroductionEvents(new InMemoryIntroductionEvents(), relay);
        using var sub = server.Subscribe("inst", "ama");
        relay.Raise(payload);
        Assert.Empty(Drain(sub));
    }
}
