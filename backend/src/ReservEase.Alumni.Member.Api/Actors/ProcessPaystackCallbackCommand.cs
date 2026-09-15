namespace ReservEase.Alumni.Member.Api.Actors;

public sealed class ProcessPaystackCallbackCommand
{
    public string Reference { get; }
    public string RawBody { get; }

    /// <summary>Id of the WebhookEvent inbox row already persisted for this delivery, so the actor can mark it processed/failed instead of losing track of it if the process dies mid-handling.</summary>
    public string WebhookEventId { get; }

    public ProcessPaystackCallbackCommand(string reference, string rawBody, string webhookEventId)
    {
        Reference = reference;
        RawBody = rawBody;
        WebhookEventId = webhookEventId;
    }
}
