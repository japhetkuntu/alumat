namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Notification : BaseEntity
{
    public string RecipientId { get; set; } = string.Empty;
    /// <summary>Member | Admin</summary>
    public string RecipientType { get; set; } = "Member";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>JobAlert | CampaignAlert | EventReminder | SpotlightUpdate | ClassNoteAlert | MembershipReminder | PaymentReceived | ContributionConfirmed | ContributionRejected</summary>
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? RelatedEntityId { get; set; }
    /// <summary>Job | Campaign | Event | Spotlight | ClassNote | Contribution</summary>
    public string? RelatedEntityType { get; set; }
    public string? ActionUrl { get; set; }
}
