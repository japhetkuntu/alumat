namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>An immutable record of a sensitive action taken by a platform staff member. Global, not tenant-scoped.</summary>
public class AuditLogEntry : BaseEntity
{
    public string? ActorId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}
