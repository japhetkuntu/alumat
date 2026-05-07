namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class MentorshipRequest : BaseEntity
{
    public string MentorProfileId { get; set; } = string.Empty;
    public MentorProfileSnapshot? MentorProfile { get; set; }
    public string MenteeId { get; set; } = string.Empty;
    public MemberSnapshot? Mentee { get; set; }
    public string Area { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected
}
