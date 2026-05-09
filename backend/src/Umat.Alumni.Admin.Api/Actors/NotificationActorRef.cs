using Akka.Actor;
using Umat.Alumni.Admin.Api.Services.Interfaces;

namespace Umat.Alumni.Admin.Api.Actors;

/// <summary>Singleton wrapper that exposes the notification dispatcher actor as INotificationActor for DI.</summary>
public sealed class NotificationActorRef(IActorRef actorRef) : INotificationActor
{
    public void Tell(object message) => actorRef.Tell(message);
}
