using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A sub-group inside an institution's alumni network (e.g. "Mining Engineering
/// Alumni") — created by institution admins; members request to join and, once
/// approved, see and participate in that community's own scoped content
/// (starting with Forum; other features follow the same CommunityId pattern).
/// The community's existence and basic info are publicly listable to every
/// member, but its actual content is members-only — see CommunityMembership.
/// </summary>
public class Community : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
