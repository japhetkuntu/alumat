using Akka.Actor;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;

namespace ReservEase.Alumni.Institution.Api.Actors;

/// <summary>Singleton wrapper that exposes the notification dispatcher actor as INotificationActor for DI.</summary>
public sealed class NotificationActorRef(IActorRef actorRef) : INotificationActor
{
    public void Tell(object message) => actorRef.Tell(message);
}
