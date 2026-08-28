namespace ReservEase.Alumni.Member.Api.Models;

// ShowOnWallOfSupport is opt-in only — whether the giver's name (never the amount) may appear on this campaign's public Wall of support.
public record InitiatePaystackPaymentRequest(
    string CampaignId, decimal Amount, string? Email = null, string? CallbackUrl = null, string? SharedByMemberId = null,
    bool ShowOnWallOfSupport = false);
