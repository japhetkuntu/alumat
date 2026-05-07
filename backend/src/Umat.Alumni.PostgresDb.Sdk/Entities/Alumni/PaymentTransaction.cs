namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class PaymentTransaction : BaseEntity
{
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }

    public string CampaignId { get; set; } = string.Empty;
    public CampaignSnapshot? Campaign { get; set; }

    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed

    public string? PaymentMethod { get; set; }
    public string? GatewayResponse { get; set; }
    public string? Channel { get; set; }
    public string? Currency { get; set; }
    public int? MembershipYears { get; set; }
    public string? FailureMessage { get; set; }

    // Raw payload as received from Paystack for debugging/inspection.
    public string? CallbackPayload { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
