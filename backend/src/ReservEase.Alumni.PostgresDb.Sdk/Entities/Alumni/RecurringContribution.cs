using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A member's standing "give ₵X every month" commitment to one campaign,
/// charged automatically by RecurringGivingSchedulerService via Paystack's
/// charge_authorization endpoint against a saved, reusable card token — no
/// separate Paystack Plan/Subscription object involved, so the existing
/// Zero-Deduction split (BuildZeroDeductionCharge/ResolveSubaccountAsync) and
/// per-institution subaccount routing apply exactly as they do to a one-off
/// contribution.
/// </summary>
public class RecurringContribution : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string CampaignId { get; set; } = string.Empty;
    public CampaignSnapshot? Campaign { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Active (charges on schedule), Paused (campaign closed or member paused — not charged, resumable), Cancelled (member stopped it — terminal), Failed (3 consecutive failed charge attempts — terminal, member must set up a new one).</summary>
    public string Status { get; set; } = "Active";

    /// <summary>The reusable card token from the first, member-present charge — captured from Paystack's authorization object, re-used for every off-session charge.</summary>
    public string AuthorizationCode { get; set; } = string.Empty;
    public string? CardLast4 { get; set; }
    public string? CardType { get; set; }
    public string? CardBank { get; set; }

    public DateTime NextChargeDate { get; set; }
    public DateTime? LastChargeAt { get; set; }
    public string? LastChargeStatus { get; set; }

    /// <summary>Consecutive failed charge attempts — reset to 0 on any success, and the row moves to Failed once this hits 3.</summary>
    public int FailedAttemptCount { get; set; }
}
