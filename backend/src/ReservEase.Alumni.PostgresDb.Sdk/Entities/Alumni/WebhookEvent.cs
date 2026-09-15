namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// Inbox record for an inbound payment-provider webhook, written before the event is
/// handed off for async processing. A provider's webhook is a fixed platform-wide URL
/// with no per-institution routing, and delivery is at-least-once — so this table is
/// deliberately not tenant-scoped, and exists to survive a crash between "received"
/// and "processed" (nothing is lost) and to let a duplicate delivery for the same
/// Reference be recognized before it's re-applied.
/// </summary>
public class WebhookEvent : BaseEntity
{
    public string Provider { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string RawBody { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
