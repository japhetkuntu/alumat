using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Spotlight : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    // The primary/first celebrant — always populated, including for a
    // multi-person Birthday spotlight (the first person in MemberIds),
    // so every existing single-member read path keeps working unchanged.
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }

    // "Manual" (the existing admin/self-submitted flow, untouched — these
    // stay empty) or "Birthday" (system-generated; see
    // BirthdaySpotlightSchedulerService). A Birthday spotlight can cover
    // more than one member at once when several share a birthday — MemberId/
    // Member above hold the first of them, MemberIds/Members hold everyone.
    public string Type { get; set; } = "Manual";
    public List<string> MemberIds { get; set; } = [];
    public List<MemberSnapshot> Members { get; set; } = [];

    public string Title { get; set; } = string.Empty;
    public string Story { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime? FeaturedMonth { get; set; }
    public string? AdminNotes { get; set; }
}
