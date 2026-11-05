using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ReservEase.Alumni.Member.Api.Services.Realtime;

/// <summary>
/// A notice that something changed in an introduction. It never carries names or message text: the page that receives it asks the
/// normal, permission-checked endpoints for the content.
/// </summary>
/// <param name="Kind">request, accepted, message, read, closed (the other side ended or removed it) or changed (something I did elsewhere).</param>
public sealed record IntroductionEvent(string Kind, string IntroductionId);

/// <summary>Tells members' open pages that something changed, without them having to keep asking.</summary>
public interface IIntroductionEvents
{
    /// <summary>Opens a listener for one member. Dispose it when the connection ends.</summary>
    IntroductionSubscription Subscribe(string institutionId, string memberId);
    /// <summary>Tells every open connection of one member. Never throws and never waits for a slow reader.</summary>
    void Publish(string institutionId, string memberId, IntroductionEvent e);
}

public sealed class IntroductionSubscription(ChannelReader<IntroductionEvent> reader, Action onDispose) : IDisposable
{
    public ChannelReader<IntroductionEvent> Reader { get; } = reader;
    public void Dispose() => onDispose();
}

/// <summary>
/// Keeps who is listening in this process's memory, which is right while the API runs as one process (as it does today). With more
/// than one API process this one class is what changes (to relay through Redis); nothing that uses it does.
/// </summary>
public sealed class InMemoryIntroductionEvents : IIntroductionEvents
{
    /// <summary>Tabs a member can have open at once. The oldest closes when another opens, so one person cannot hold unlimited connections.</summary>
    public const int MaxPerMember = 5;
    private const int QueueSize = 64;

    private sealed class Listener(Channel<IntroductionEvent> channel) { public Channel<IntroductionEvent> Channel { get; } = channel; }
    private readonly ConcurrentDictionary<string, List<Listener>> _listeners = new();

    private static string Key(string institutionId, string memberId) => $"{institutionId}:{memberId}";

    public int OpenConnections => _listeners.Values.Sum(l => { lock (l) return l.Count; });

    public IntroductionSubscription Subscribe(string institutionId, string memberId)
    {
        var key = Key(institutionId, memberId);
        // A burst larger than the queue drops the oldest notice: the page refreshes from the server on each notice anyway, so a dropped one costs nothing.
        var channel = Channel.CreateBounded<IntroductionEvent>(new BoundedChannelOptions(QueueSize) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        var listener = new Listener(channel);
        var list = _listeners.GetOrAdd(key, _ => new List<Listener>());
        lock (list)
        {
            list.Add(listener);
            while (list.Count > MaxPerMember) { list[0].Channel.Writer.TryComplete(); list.RemoveAt(0); }
        }
        return new IntroductionSubscription(channel.Reader, () =>
        {
            channel.Writer.TryComplete();
            lock (list)
            {
                list.Remove(listener);
                if (list.Count == 0) _listeners.TryRemove(new KeyValuePair<string, List<Listener>>(key, list));
            }
        });
    }

    public void Publish(string institutionId, string memberId, IntroductionEvent e)
    {
        if (!_listeners.TryGetValue(Key(institutionId, memberId), out var list)) return;
        Listener[] now;
        lock (list) now = list.ToArray();
        foreach (var l in now) l.Channel.Writer.TryWrite(e);
    }
}
