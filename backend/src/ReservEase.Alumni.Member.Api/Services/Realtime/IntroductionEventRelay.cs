using System.Text.Json;
using StackExchange.Redis;

namespace ReservEase.Alumni.Member.Api.Services.Realtime;

/// <summary>Carries notices between API servers. Not used when there is only one.</summary>
public interface IEventRelay
{
    void Send(string payload);
    /// <summary>Raised for every payload sent by <b>another</b> server.</summary>
    event Action<string>? Received;
}

/// <summary>
/// The in-memory hub plus a relay, so a member connected to one API server hears about something that happened on another.
/// Local delivery is immediate and does not depend on the relay; the relay only adds the other servers. If Redis is unreachable, members
/// on the same server still get everything and only cross-server notices are missed (the page's reconnect catch-up covers those).
/// </summary>
public sealed class RelayedIntroductionEvents : IIntroductionEvents
{
    private sealed record Wire(string I, string M, string K, string Id);   // institution, member, kind, introduction
    private readonly IIntroductionEvents _local;
    private readonly IEventRelay _relay;

    public RelayedIntroductionEvents(IIntroductionEvents local, IEventRelay relay)
    {
        _local = local;
        _relay = relay;
        _relay.Received += OnRemote;
    }

    public IntroductionSubscription Subscribe(string institutionId, string memberId) => _local.Subscribe(institutionId, memberId);

    public void Publish(string institutionId, string memberId, IntroductionEvent e)
    {
        _local.Publish(institutionId, memberId, e);
        try { _relay.Send(JsonSerializer.Serialize(new Wire(institutionId, memberId, e.Kind, e.IntroductionId))); }
        catch { /* the relay being down must never break sending a message */ }
    }

    private void OnRemote(string payload)
    {
        try
        {
            var w = JsonSerializer.Deserialize<Wire>(payload);
            if (w is not null && !string.IsNullOrEmpty(w.M)) _local.Publish(w.I, w.M, new IntroductionEvent(w.K, w.Id));
        }
        catch { /* a malformed relay message is ignored */ }
    }
}

/// <summary>Redis publish/subscribe as the relay. One small channel for all notices; each server ignores what it sent itself.</summary>
public sealed class RedisEventRelay : BackgroundService, IEventRelay
{
    private const string Channel = "alumni:introduction-events";
    private readonly string _connection;
    private readonly string _self = Guid.NewGuid().ToString("N");   // identifies this server so it can skip its own messages
    private readonly ILogger<RedisEventRelay> _logger;
    private volatile ISubscriber? _subscriber;

    public RedisEventRelay(string connectionString, ILogger<RedisEventRelay> logger) { _connection = connectionString; _logger = logger; }

    public event Action<string>? Received;

    public void Send(string payload)
    {
        // Fire and forget: nothing waits for Redis, and nothing queues up if it is down.
        _subscriber?.Publish(RedisChannel.Literal(Channel), $"{_self}|{payload}", CommandFlags.FireAndForget);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var options = ConfigurationOptions.Parse(_connection);
            options.AbortOnConnectFail = false;   // start without Redis and join it when it appears
            var mux = await ConnectionMultiplexer.ConnectAsync(options);
            var sub = mux.GetSubscriber();
            await sub.SubscribeAsync(RedisChannel.Literal(Channel), (_, value) =>
            {
                var text = (string?)value;
                if (text is null) return;
                var cut = text.IndexOf('|');
                if (cut <= 0 || text.AsSpan(0, cut).SequenceEqual(_self)) return;   // my own message coming back
                Received?.Invoke(text[(cut + 1)..]);
            });
            _subscriber = sub;
            _logger.LogInformation("Introduction notices are relayed between servers through Redis");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception e) { _logger.LogError(e, "The Redis relay for introduction notices could not start; notices stay on this server"); }
    }
}
