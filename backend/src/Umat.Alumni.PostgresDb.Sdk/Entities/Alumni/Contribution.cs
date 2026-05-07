namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Contribution : BaseEntity
{
    public string CampaignId { get; set; } = string.Empty;
    public CampaignSnapshot? Campaign { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Rejected
    public string? TransactionRef { get; set; }
    public string? ProofUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
}
