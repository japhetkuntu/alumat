using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Institution.Api.Models;

public record BatchListItem(
    string Id, string Name, int Year, bool IsActive,
    string PayoutStatus, bool UseInstitutionAccount,
    string? SettlementBankName, string? SettlementAccountNumber, string? SettlementAccountName);

public class CreateBatchRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1900, 2200)]
    public int Year { get; set; }
}

public class UpdateBatchRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1900, 2200)]
    public int Year { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Submitted by a SuperAdmin (any batch) or the ScopedAdmin assigned to this
/// batch. Never writes the batch's live settlement fields directly — lands in
/// Batch.PendingPayoutChanges until a platform staffer approves it (see
/// Platform.Api's BatchPayoutsController).
/// </summary>
public class SubmitBatchPayoutSetupRequest
{
    /// <summary>True = settle into the institution's own Paystack subaccount instead of a dedicated one for this batch — the fields below are ignored.</summary>
    public bool UseInstitutionAccount { get; set; }
    public string? SettlementBankCode { get; set; }
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }
}

public record BankOption(string Name, string Code);
public record ResolvedAccountResponse(string AccountNumber, string AccountName);
