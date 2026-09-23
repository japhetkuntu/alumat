namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One browser/device Web Push subscription for a member or staff account.
/// A single owner can have several rows (one per browser/device) — unlike
/// SmsAlerts/WhatsAppAlerts on <see cref="NotificationPreference"/>, there is no
/// separate opt-in boolean for push: an active row here IS the opt-in signal.
/// </summary>
public class PushSubscription : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;
    public string OwnerType { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;
    public string P256dhKey { get; set; } = string.Empty;
    public string AuthKey { get; set; } = string.Empty;

    public string? UserAgent { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? LastFailedAt { get; set; }

    /// <summary>Soft-disabled (rather than deleted) when the push service reports 410 Gone / 404.</summary>
    public bool IsActive { get; set; } = true;
}

public static class PushSubscriptionOwnerTypes
{
    public const string Member = "Member";
    public const string InstitutionStaff = "InstitutionStaff";
}
