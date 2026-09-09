namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>An institution whose payout setup (see Institution.Api's InstitutionController.SubmitPayoutSetup) is awaiting review.</summary>
public record PendingInstitutionPayoutItem(
    string InstitutionId, string InstitutionName,
    string? SettlementBankName, string? SettlementAccountNumber, string? SettlementAccountName,
    DateTime SubmittedAt);

public class RejectInstitutionPayoutRequest
{
    public string? Notes { get; set; }
}
