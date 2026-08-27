namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class MentorProfileSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }
    public string Area { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public int MaxMentees { get; set; }
    public int CurrentMenteeCount { get; set; }
    public string Status { get; set; } = string.Empty;

    public string? ContactLinkedInUrl { get; set; }
    public string? ContactWhatsAppNumber { get; set; }
    public string? ContactPhoneNumber { get; set; }
}
