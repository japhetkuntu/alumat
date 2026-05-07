namespace Umat.Alumni.Member.Api.Models;

public record InitiatePaystackPaymentRequest(string CampaignId, decimal Amount, string? Email = null, string? CallbackUrl = null);
