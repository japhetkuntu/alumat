using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class PaymentTransaction : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }

    /// <summary>
    /// True when the payer was not logged in at the time of payment. MemberId may
    /// still be set for a guest payment — either matched by email to a pending
    /// member, or attributed to whoever shared the campaign link (SharedByMemberId).
    /// </summary>
    public bool IsGuestPayment { get; set; }
    /// <summary>
    /// The member who shared the campaign link the guest paid through, if any —
    /// distinct from MemberId, which is who the payment is attributed to.
    /// </summary>
    public string? SharedByMemberId { get; set; }

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
