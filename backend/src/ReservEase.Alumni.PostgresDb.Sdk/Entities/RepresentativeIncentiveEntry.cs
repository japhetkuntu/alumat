namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// One line in a representative's incentive ledger. Settlement to representatives is entered by hand: nothing here moves
/// money. "Earned" is something the platform owes them; "Paid" is a payment a SuperAdmin made outside the system and recorded.
/// Entries are never edited or deleted — a mistake is voided with a reason, so the history stays honest.
/// </summary>
public class RepresentativeIncentiveEntry : BaseEntity
{
    /// <summary>The representative (a <see cref="PlatformStaff"/> id).</summary>
    public string StaffId { get; set; } = string.Empty;
    /// <summary>"Earned" or "Paid".</summary>
    public string Kind { get; set; } = "Earned";
    public decimal Amount { get; set; }
    /// <summary>Why it was earned, or what the payment covered.</summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>For a payment: the mobile-money or bank reference the SuperAdmin paid with.</summary>
    public string? Reference { get; set; }
    /// <summary>The institution an earning is for, when it is for one.</summary>
    public string? InstitutionId { get; set; }
    /// <summary>The day it happened, which may differ from the day it was recorded.</summary>
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow.Date;
    public string RecordedByStaffId { get; set; } = string.Empty;
    public string RecordedByName { get; set; } = string.Empty;
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    public string? VoidedByName { get; set; }
}

public static class IncentiveKinds
{
    public const string Earned = "Earned";
    public const string Paid = "Paid";
    public static bool IsValid(string? k) => k is Earned or Paid;
}
