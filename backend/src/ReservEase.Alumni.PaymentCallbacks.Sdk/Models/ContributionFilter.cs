using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Models;

public class ContributionFilter : BaseFilter
{
    public string? CampaignId { get; set; }
}
