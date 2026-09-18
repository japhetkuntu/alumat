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
    // BirthdaySpotlightDispatchWorkflow (Operations.Worker)). A Birthday spotlight can cover
    // more than one member at once when several share a birthday — MemberId/
    // Member above hold the first of them, MemberIds/Members hold everyone.
    public string Type { get; set; } = "Manual";
    public List<string> MemberIds { get; set; } = [];
    public List<MemberSnapshot> Members { get; set; } = [];

    public string Title { get; set; } = string.Empty;
    public string Story { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Archived
    public DateTime? FeaturedMonth { get; set; }
    public string? AdminNotes { get; set; }

    // Explicit "show this one on the public site" pick, rather than leaving it
    // to implicit FeaturedMonth/CreatedAt ordering — an admin can promote any
    // approved spotlight to the public landing page regardless of when it was
    // created. At most one row per institution should carry this at a time;
    // enforced by InstitutionSpotlightService.SetFeaturedAsync, not the DB.
    public bool IsFeatured { get; set; }

    // Set once a Birthday-type spotlight's forum shoutout thread(s) are
    // created — one thread per celebrant (each posted as that member, see
    // BirthdaySpotlightDispatchWorkflow), indexed the same order as
    // MemberIds. Lets each celebrant's own notification link straight to
    // their thread, and stops the dispatch workflow from creating a second
    // thread for the same spotlight on retry.
    public List<string> ForumThreadIds { get; set; } = [];
}
