using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>One entry in a <see cref="ServiceRequest"/>'s fulfillment timeline — a stage change and/or a staff note, optionally with a file handed back to the member (e.g. the issued document itself).</summary>
public class ServiceRequestUpdate
{
    public DateTime ChangedAt { get; set; }
    /// <summary>The stage this update moved the request to. Null when this entry is only a note, with no stage change.</summary>
    public string? Stage { get; set; }
    public string? Note { get; set; }
    /// <summary>A file the institution is handing back to the member at this point (e.g. the issued transcript PDF).</summary>
    public string? AttachmentUrl { get; set; }
    public string? ChangedByStaffName { get; set; }
}

/// <summary>
/// One member's request for a <see cref="ServiceType"/> — the purchase (if
/// priced) and the fulfillment ticket, in one record. Same shape/lifecycle
/// as StoreOrder: created synchronously at initiation (Pending payment) or
/// immediately at Stages[0] when the service is free, then updated by the
/// Paystack webhook and by institution staff working the queue. Snapshots
/// the service's name/price at request time so a later edit to the
/// ServiceType never changes what a past request shows.
/// </summary>
public class ServiceRequest : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Short human-friendly reference, e.g. "A1B2C3D4" — same convention as StoreOrder.OrderNumber.</summary>
    public string RequestNumber { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }

    public string ServiceTypeId { get; set; } = string.Empty;
    public string ServiceTypeName { get; set; } = string.Empty;
    /// <summary>Snapshot of ServiceType.Price at request time — what this request actually charged, independent of later price changes.</summary>
    public decimal Amount { get; set; }

    /// <summary>The member's answers to ServiceType.Fields, keyed by ServiceFieldDefinition.Key.</summary>
    public Dictionary<string, string> FieldAnswers { get; set; } = [];
    /// <summary>Uploaded file URLs for File-type fields, keyed by ServiceFieldDefinition.Key.</summary>
    public Dictionary<string, string> Attachments { get; set; } = [];

    /// <summary>NotRequired (free service), Pending, Successful, or Failed.</summary>
    public string PaymentStatus { get; set; } = "Pending";
    public string PaymentMethod { get; set; } = "Paystack";
    public string? TransactionRef { get; set; }

    /// <summary>What the platform earned on this request, collected from the payer's grossed-up charge — never deducted from Amount. Internal accounting only.</summary>
    public decimal PlatformFeeAmount { get; set; }
    /// <summary>Paystack's own processing fee, absorbed by the platform's share (bearer="account"), not the institution. Internal reconciliation only.</summary>
    public decimal GatewayFeeAmount { get; set; }
    /// <summary>The flat transaction_charge routed to the platform's main account at initiation. Internal reconciliation only.</summary>
    public decimal TransactionChargeAmount { get; set; }
    /// <summary>The total amount actually charged to the payer via Paystack. Internal reconciliation only.</summary>
    public decimal GrossChargeAmount { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public string? FailureMessage { get; set; }
    public string? Channel { get; set; }
    public string? GatewayResponse { get; set; }
    public string? CallbackPayload { get; set; }

    /// <summary>
    /// Current position in ServiceType.Stages. Set to Stages[0] as soon as
    /// the request is payable/created; institution staff advance it from the
    /// queue. Deliberately no separate "rejected" status — money has already
    /// settled straight to the institution's own account by the time a
    /// request exists, so there's no refund path a "rejected" state could
    /// honestly represent. An institution that can't fulfill a request
    /// communicates that through its own stage names (e.g. "Unable to
    /// Process") plus a note, same as every other status update.
    /// </summary>
    public string CurrentStage { get; set; } = string.Empty;

    public List<ServiceRequestUpdate> Updates { get; set; } = [];
}
