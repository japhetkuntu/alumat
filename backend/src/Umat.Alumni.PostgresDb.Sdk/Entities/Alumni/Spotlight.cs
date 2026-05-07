namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Spotlight : BaseEntity
{
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Story { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime? FeaturedMonth { get; set; }
    public string? AdminNotes { get; set; }
}
