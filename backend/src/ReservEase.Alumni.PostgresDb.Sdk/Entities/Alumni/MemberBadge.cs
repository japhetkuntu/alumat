using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class MemberBadge : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }
    public string BadgeType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}
