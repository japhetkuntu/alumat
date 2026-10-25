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

/// <summary>Adds every year from <see cref="FromYear"/> to <see cref="ToYear"/> that does not have a year group yet.</summary>
public class CreateBatchRangeRequest : IValidatableObject
{
    public const int MaxYears = 150;

    [Required, Range(1900, 2200)]
    public int FromYear { get; set; }

    [Required, Range(1900, 2200)]
    public int ToYear { get; set; }

    /// <summary>How each one is named. "{year}" is replaced by the year; without it the year is added to the end, so "Class of" gives "Class of 2016".</summary>
    [Required, MaxLength(100)]
    public string NamePattern { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ToYear < FromYear)
            yield return new ValidationResult("The last year must not be before the first.", [nameof(ToYear)]);
        else if (ToYear - FromYear + 1 > MaxYears)
            yield return new ValidationResult($"Add at most {MaxYears} years at a time.", [nameof(ToYear)]);
    }
}

/// <param name="Created">How many year groups were added.</param>
/// <param name="SkippedYears">Years in the range that already had a year group, left exactly as they were.</param>
public record CreateBatchRangeResult(int Created, List<int> SkippedYears);

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
