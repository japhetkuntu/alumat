using ReservEase.Alumni.Paystack.Sdk.Models;

namespace ReservEase.Alumni.Operations.Worker.Models;

public sealed class ContributionPaystackVerifyResult
{
    public bool Status { get; init; }
    public string? Message { get; init; }
    public string PaystackStatus { get; init; } = "unknown";
    public decimal GrossAmount { get; init; }
    public decimal? GatewayFee { get; init; }
    public string? GatewayResponse { get; init; }
    public PaystackAuthorization? Authorization { get; init; }
}

public sealed class MembershipReevalData
{
    public required List<string> RequiredCampaignIds { get; init; }
    public required List<string> PaidCampaignIds { get; init; }
    public string? MembershipActivePolicy { get; init; }
}
