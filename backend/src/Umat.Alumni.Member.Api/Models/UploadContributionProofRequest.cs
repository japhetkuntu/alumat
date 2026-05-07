namespace Umat.Alumni.Member.Api.Models;

public record UploadContributionProofRequest(string CampaignId, string TransactionRef, string? Notes);
