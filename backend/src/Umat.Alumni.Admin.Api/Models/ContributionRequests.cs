namespace Umat.Alumni.Admin.Api.Models;

public record RecordManualContributionRequest(
    string CampaignId,
    string MemberNumber,
    string? MemberName,
    string? MemberEmail,
    decimal Amount,
    string PaymentMethod,
    string? TransactionRef,
    string? Notes,
    DateTime? PaidAt,
    bool Confirmed);
public record ConfirmContributionRequest(string ContributionId);
public record RejectContributionRequest(string ContributionId, string? Reason);
