namespace ReservEase.Alumni.Member.Api.Models;

public record UploadContributionProofRequest(string CampaignId, string TransactionRef, string? Notes);
