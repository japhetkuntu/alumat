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
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Rejected
    public string? TransactionRef { get; set; }
    public string? ProofUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }

    /// <summary>
    /// The platform's cut of this contribution, computed and stored at
    /// confirmation time from the institution's PlatformFeePercentage at that
    /// moment — never recomputed later, so revenue reporting stays accurate
    /// even after an institution's fee percentage is later edited. Zero for
    /// contributions confirmed before this field existed, and for manual
    /// (offline) payments, which never route through Paystack's split.
    /// </summary>
    public decimal PlatformFeeAmount { get; set; }
    /// <summary>Amount - PlatformFeeAmount — what actually settles to the institution.</summary>
    public decimal NetAmountToInstitution { get; set; }

    /// <summary>True when the payer was not logged in at the time of payment.</summary>
    public bool IsGuestPayment { get; set; }
    /// <summary>The member who shared the campaign link this guest paid through, if any.</summary>
    public string? SharedByMemberId { get; set; }
}
