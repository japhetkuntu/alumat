using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

public class AddIncentiveEntryRequest
{
    [Required] public string StaffId { get; set; } = string.Empty;
    /// <summary>"Earned" or "Paid".</summary>
    [Required, RegularExpression("^(Earned|Paid)$")] public string Kind { get; set; } = string.Empty;
    [Range(0.01, 1_000_000)] public decimal Amount { get; set; }
    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
    [MaxLength(120)] public string? Reference { get; set; }
    public string? InstitutionId { get; set; }
    public DateTime? OccurredOn { get; set; }
}

public class VoidIncentiveEntryRequest
{
    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
}

public record IncentiveEntryDto(
    string Id, string StaffId, string StaffName, string Kind, decimal Amount, string Reason, string? Reference,
    string? InstitutionId, string? InstitutionName, DateTime OccurredOn, string RecordedByName, DateTime CreatedAt,
    bool IsVoided, string? VoidReason, string? VoidedByName);

/// <summary>What one representative has earned, been paid, and is still owed. Voided entries are left out of the totals.</summary>
public record IncentiveBalanceDto(string StaffId, string StaffName, decimal Earned, decimal Paid, decimal Owed, DateTime? LastPaidOn);

public record IncentivesViewDto(
    bool CanRecord, string Currency, List<IncentiveBalanceDto> Balances, List<IncentiveEntryDto> Entries);
