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
    /// <summary>Retired: the WhatsApp number was removed from the product. The column stays so no data is dropped; nothing reads or writes it.</summary>
    public string? ContactWhatsAppNumber { get; set; }
    public string? ContactPhoneNumber { get; set; }
}
