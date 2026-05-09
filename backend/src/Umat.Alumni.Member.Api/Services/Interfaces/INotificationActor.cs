namespace Umat.Alumni.Member.Api.Services.Interfaces;

/// <summary>
/// DI abstraction over the Akka notification dispatcher actor.
/// Services call Tell() to enqueue a notification command without blocking.
/// </summary>
public interface INotificationActor
{
    void Tell(object message);
}
