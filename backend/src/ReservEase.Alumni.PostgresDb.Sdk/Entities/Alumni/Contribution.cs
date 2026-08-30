using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Contribution : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string CampaignId { get; set; } = string.Empty;
    public CampaignSnapshot? Campaign { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Successful, Rejected
    public string? TransactionRef { get; set; }
    public string? ProofUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }

    /// <summary>
    /// Under the Zero-Deduction model this is always 0 for online (Paystack)
    /// payments — our platform fee is collected from the payer's grossed-up
    /// charge (see PlatformRevenueAmount), never deducted from the
    /// institution's Amount. Kept for historical contributions recorded
    /// before this model existed, and untouched for manual (offline)
    /// payments, which never route through Paystack's split.
    /// </summary>
    public decimal PlatformFeeAmount { get; set; }
    /// <summary>Amount - PlatformFeeAmount — always equal to Amount for Zero-Deduction (Paystack) payments, since nothing is ever deducted from the institution.</summary>
    public decimal NetAmountToInstitution { get; set; }

    /// <summary>
    /// What the platform actually earned on this contribution, collected from
    /// the payer's grossed-up charge — never deducted from Amount or
    /// NetAmountToInstitution. Zero for manual/offline payments and for
    /// contributions confirmed before the Zero-Deduction model existed.
    /// Internal accounting only — never exposed via ContributionDto.
    /// </summary>
    public decimal PlatformRevenueAmount { get; set; }
    /// <summary>
    /// The total amount actually charged to the payer via Paystack (Amount +
    /// PlatformRevenueAmount + GatewayFeeAmount). Internal reconciliation
    /// only — never exposed via ContributionDto.
    /// </summary>
    public decimal GrossChargeAmount { get; set; }
    /// <summary>Paystack's own processing fee for this transaction, absorbed entirely by the payer. Internal reconciliation only.</summary>
    public decimal GatewayFeeAmount { get; set; }

    /// <summary>True when the payer was not logged in at the time of payment.</summary>
    public bool IsGuestPayment { get; set; }
    /// <summary>The member who shared the campaign link this guest paid through, if any.</summary>
    public string? SharedByMemberId { get; set; }

    /// <summary>
    /// Opt-in only, set by the giver at payment time — never inferred. When
    /// true, this contribution's giver name (never the amount) may appear on
    /// the campaign's public "Wall of support". Amounts are never shown to
    /// anyone but the giver and institution staff, regardless of this flag.
    /// </summary>
    public bool ShowOnWallOfSupport { get; set; }
}
