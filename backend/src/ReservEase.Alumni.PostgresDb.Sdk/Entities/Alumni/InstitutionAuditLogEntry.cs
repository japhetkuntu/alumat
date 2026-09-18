using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// An immutable record of a sensitive action taken by one of this institution's own admins —
/// so a SuperAdmin can see what every admin (including other SuperAdmins and ScopedAdmins) has
/// done, since an institution can have several. Tenant-scoped (unlike the platform-staff
/// AuditLogEntry, which is global) and mirrors it field-for-field otherwise. High-value actions
/// only, by design: member approve/reject/ban, staff created/updated (role or disabled-state
/// changes included), payout/settlement changes, and content deletion — not every routine edit.
/// </summary>
public class InstitutionAuditLogEntry : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string? ActorId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}
