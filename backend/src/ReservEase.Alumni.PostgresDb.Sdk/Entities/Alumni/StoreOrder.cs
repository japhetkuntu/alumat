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

    /// <summary>Null when the product was simple (no variants) at purchase time.</summary>
    public string? VariantId { get; set; }
    /// <summary>Snapshot of the variant's option values at purchase time, e.g. {"Size":"Medium"}.</summary>
    public Dictionary<string, string>? VariantOptions { get; set; }
    public string? Sku { get; set; }

    /// <summary>What the buyer filled in for this item (questions and delivery details), with the labels as they were at purchase time.</summary>
    public List<StoreOrderItemAnswer> Answers { get; set; } = [];

    /// <summary>Snapshot of the product's stages at purchase time. Empty means the order-level delivery status applies instead.</summary>
    public List<string> Stages { get; set; } = [];
    /// <summary>Current position in <see cref="Stages"/>. Null when <see cref="Stages"/> is empty.</summary>
    public string? CurrentStage { get; set; }
    /// <summary>Stage changes and staff notes for this item, newest last.</summary>
    public List<ServiceRequestUpdate> Updates { get; set; } = [];
}

/// <summary>One answer a buyer gave while ordering an item — stored with its label so a later edit to the product's form never changes what a past order shows.</summary>
public class StoreOrderItemAnswer
{
    /// <summary>"Details" or "Delivery".</summary>
    public string Section { get; set; } = "Details";
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Text, TextArea, Number, Date, Select or File — File answers hold the uploaded file's URL in <see cref="Value"/>.</summary>
    public string Type { get; set; } = "Text";
    public string Value { get; set; } = string.Empty;
}

/// <summary>One entry in a <see cref="StoreOrder"/>'s delivery status timeline.</summary>
public class StoreOrderDeliveryEvent
{
    public string Status { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
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

    /// <summary>Short human-friendly reference, e.g. "A1B2C3D4" — generated at creation from Id, shown to members/staff instead of the raw GUID/Paystack reference.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }

    public List<StoreOrderItem> Items { get; set; } = [];

    /// <summary>Sum of each item's UnitPrice * Quantity — what the institution nets in full, same as Contribution.Amount.</summary>
    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, Successful, Failed
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

    /// <summary>Paystack authorization channel (e.g. "card", "mobile_money"), parsed from the webhook payload when available. Mirrors PaymentTransaction.Channel.</summary>
    public string? Channel { get; set; }
    /// <summary>Paystack's own human-readable result message for this transaction. Mirrors PaymentTransaction.GatewayResponse.</summary>
    public string? GatewayResponse { get; set; }
    /// <summary>Raw payload as received from Paystack, for debugging/inspection — only populated when confirmed via webhook, not via the polling fallback. Mirrors PaymentTransaction.CallbackPayload.</summary>
    public string? CallbackPayload { get; set; }

    /// <summary>Null until an admin sets it — stays fully optional, institutions that don't track delivery just never touch these fields.</summary>
    public string? DeliveryStatus { get; set; }
    public DateTime? DeliveryStatusUpdatedAt { get; set; }
    /// <summary>Timeline of every DeliveryStatus change — powers a member-facing tracking view.</summary>
    public List<StoreOrderDeliveryEvent> DeliveryStatusHistory { get; set; } = [];
}
