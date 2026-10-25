namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>A group whose payout setup is Pending platform review, with enough
/// institution/group context to review it without a second lookup.</summary>
public record PendingGroupPayoutItem(
    string CommunityId, string GroupName,
    string InstitutionId, string InstitutionName,
    bool UseInstitutionAccount,
    string? SettlementBankName, string? SettlementAccountNumber, string? SettlementAccountName,
    DateTime SubmittedAt);

public class RejectGroupPayoutRequest
{
    public string? Notes { get; set; }
}
