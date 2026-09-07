using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A graduating-class year group an institution admin has created (e.g. "Batch
/// of 2020"), replacing the old client-hardcoded 1952-to-current-year range on
/// the registration form. Deliberately not a foreign key target — Member.GraduationYear
/// stays a plain int matched by value, so removing a Batch never orphans a member record.
/// </summary>
public class Batch : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// None (never set up) — Pending (submitted, awaiting platform staff
    /// review) — Approved (live, PaystackSubaccountCode below is real and
    /// used at charge time) — Rejected (platform staff declined; the
    /// institution's own account is used instead, same as None).
    /// </summary>
    public string PayoutStatus { get; set; } = "None";

    /// <summary>True = this batch settles into the institution's own Paystack
    /// subaccount (no dedicated account of its own); false = it has its own
    /// settlement details below, once Approved.</summary>
    public bool UseInstitutionAccount { get; set; }

    /// <summary>Live Paystack subaccount code — only set once Approved. Same
    /// platform fee percentage as the institution (Institution.PlatformFeePercentage);
    /// there is no separate per-batch fee.</summary>
    public string? PaystackSubaccountCode { get; set; }
    public string? SettlementBankCode { get; set; }
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }

    /// <summary>
    /// Proposed values from the batch's most recent payout-setup submission,
    /// staged here until a platform staffer approves or rejects them — the
    /// live fields above are only ever written by that approval, never
    /// directly by the institution-side submission endpoint. Cleared on
    /// either approve or reject.
    /// </summary>
    public BatchPayoutPendingChanges? PendingPayoutChanges { get; set; }
}

public class BatchPayoutPendingChanges
{
    public bool UseInstitutionAccount { get; set; }
    public string? SettlementBankCode { get; set; }
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }
}
