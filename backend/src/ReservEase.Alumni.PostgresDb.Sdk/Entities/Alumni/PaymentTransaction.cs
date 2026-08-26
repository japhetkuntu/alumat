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
    /// <summary>The intended amount — what the campaign/membership actually costs, and what the institution nets. Never overwritten by Paystack's gross charge.</summary>
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed

    // ── Zero-Deduction breakdown (Paystack payments only) ───────────────────
    /// <summary>Our target platform fee for this transaction, computed at initiation from the institution's PlatformFeePercentage. 0 for non-split payments.</summary>
    public decimal PlatformFeeAmount { get; set; }
    /// <summary>Paystack's processing fee — the real figure reported by /transaction/verify once available, else our estimate from initiation. 0 for non-split payments.</summary>
    public decimal GatewayFeeAmount { get; set; }
    /// <summary>
    /// The fixed amount routed to our main platform account (Paystack's
    /// transaction_charge), set at initiation and never changed — includes
    /// PlatformFeeAmount, our gateway-fee estimate, and the safety buffer.
    /// Once the real GatewayFeeAmount is known post-verification, our actual
    /// net revenue is TransactionChargeAmount - GatewayFeeAmount(real). 0 for
    /// non-split payments.
    /// </summary>
    public decimal TransactionChargeAmount { get; set; }
    /// <summary>The total actually charged to the payer (Amount + PlatformFeeAmount + GatewayFeeAmount + safety buffer). Equal to Amount for non-split payments.</summary>
    public decimal GrossChargeAmount { get; set; }

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
