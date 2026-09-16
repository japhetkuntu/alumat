namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Models;

public record UploadContributionProofRequest(string CampaignId, string TransactionRef, string? Notes);
