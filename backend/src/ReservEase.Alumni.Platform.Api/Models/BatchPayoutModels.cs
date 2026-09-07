namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>A batch whose payout setup is Pending platform review, with enough
/// institution/batch context to review it without a second lookup.</summary>
public record PendingBatchPayoutItem(
    string BatchId, string BatchName, int Year,
    string InstitutionId, string InstitutionName,
    bool UseInstitutionAccount,
    string? SettlementBankName, string? SettlementAccountNumber, string? SettlementAccountName,
    DateTime SubmittedAt);

public class RejectBatchPayoutRequest
{
    public string? Notes { get; set; }
}
