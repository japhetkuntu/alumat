using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>Values of <see cref="EventRsvp.Status"/>.</summary>
public static class EventRsvpStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    /// <summary>A paid ticket whose payment has been started but not yet confirmed. Holds a seat for a short time, never counts as attending.</summary>
    public const string PendingPayment = "PendingPayment";
    /// <summary>A paid ticket whose payment did not go through.</summary>
    public const string PaymentFailed = "PaymentFailed";
}

public class EventRsvp : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    [NotMapped]
    public EventSnapshot? Event { get; set; }

    public string MemberId { get; set; } = string.Empty;

    [NotMapped]
    public MemberSnapshot? Member { get; set; }

    public string Status { get; set; } = "Confirmed";

    // ── Paid tickets (all empty for a free RSVP) ──────────────────────────────

    /// <summary>The ticket price paid, as set on the event when it was bought. Null for a free RSVP. The institution receives all of it.</summary>
    public decimal? AmountPaid { get; set; }

    /// <summary>Paystack reference for this ticket's payment (ET_…).</summary>
    public string? TransactionRef { get; set; }

    public DateTime? PaidAt { get; set; }

    // Internal accounting only, same model as StoreOrder: never shown to institutions or members.
    public decimal PlatformFeeAmount { get; set; }
    public decimal GatewayFeeAmount { get; set; }
    public decimal TransactionChargeAmount { get; set; }
    public decimal GrossChargeAmount { get; set; }

    public string? Channel { get; set; }
    public string? GatewayResponse { get; set; }
    public string? CallbackPayload { get; set; }
    public string? FailureMessage { get; set; }

    /// <summary>True when this RSVP is a ticket that was bought, as opposed to a free RSVP.</summary>
    [NotMapped]
    public bool IsPaidTicket => AmountPaid.HasValue && AmountPaid.Value > 0;
}
