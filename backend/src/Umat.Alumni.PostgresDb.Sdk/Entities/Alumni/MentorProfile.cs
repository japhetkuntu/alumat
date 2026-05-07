namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class MentorProfile : BaseEntity
{
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }
    public string Area { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public int MaxMentees { get; set; }
    public int CurrentMenteeCount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Paused
    public List<int>? YearGroups { get; set; }
}
