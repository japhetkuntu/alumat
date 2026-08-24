using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One member's relationship to one community — a request-to-join workflow,
/// not instant membership. A "Leader" is a regular member elevated to
/// moderate just this one community (approve/reject requests, remove
/// members) — a scoped role that lives entirely in this table, since there's
/// no institution-wide admin claim involved and leader status can change
/// without the member re-logging in, so it's always checked live, never
/// carried as a JWT claim.
/// </summary>
public class CommunityMembership : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string CommunityId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;

    /// <summary>Pending, Approved, Rejected.</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Member, Leader.</summary>
    public string Role { get; set; } = "Member";

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
}
