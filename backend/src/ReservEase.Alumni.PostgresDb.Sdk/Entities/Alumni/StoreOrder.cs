using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One line item in a <see cref="StoreOrder"/> — a snapshot of what was
/// bought at the time of purchase, so later edits/deletion of the
/// StoreProduct never change what a past order shows.
/// </summary>
public class StoreOrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    /// <summary>Snapshot of the product's delivery instructions at purchase time.</summary>
    public string? DeliveryInfo { get; set; }
}

/// <summary>
/// One checkout — a single Paystack payment covering one or more
/// <see cref="StoreOrderItem"/> lines (a cart). Same Zero-Deduction
/// platform-fee model as Contribution: the institution's <see cref="TotalAmount"/>
/// is never reduced by our fee — our cut is collected from the payer's
/// grossed-up charge (see PaystackFeeCalculator / ContributionService's
/// BuildZeroDeductionCharge, which StoreOrderService mirrors).
/// </summary>
public class StoreOrder : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }

    public List<StoreOrderItem> Items { get; set; } = [];

    /// <summary>Sum of each item's UnitPrice * Quantity — what the institution nets in full, same as Contribution.Amount.</summary>
    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Failed
    public string PaymentMethod { get; set; } = "Paystack";
    public string? TransactionRef { get; set; }

    /// <summary>What the platform earned on this order, collected from the payer's grossed-up charge — never deducted from TotalAmount. Internal accounting only.</summary>
    public decimal PlatformFeeAmount { get; set; }
    /// <summary>Paystack's own processing fee, absorbed by the platform's share (bearer="account"), not the institution. Internal reconciliation only.</summary>
    public decimal GatewayFeeAmount { get; set; }
    /// <summary>The flat transaction_charge routed to the platform's main account at initiation. Internal reconciliation only.</summary>
    public decimal TransactionChargeAmount { get; set; }
    /// <summary>The total amount actually charged to the payer via Paystack. Internal reconciliation only.</summary>
    public decimal GrossChargeAmount { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public string? FailureMessage { get; set; }
}
