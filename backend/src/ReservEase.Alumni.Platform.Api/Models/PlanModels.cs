using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

public class CreatePlanRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string BillingInterval { get; set; } = "monthly";
    public int? MemberLimit { get; set; }
    public int? StorageLimitGb { get; set; }
    public List<string> Modules { get; set; } = [];
    public string SupportLevel { get; set; } = "Standard support";
    public bool IsMostUsed { get; set; }
}

public record PlanResponse(
    string Id, string Name, decimal? Price, string BillingInterval,
    int? MemberLimit, int? StorageLimitGb, List<string> Modules,
    string SupportLevel, bool IsMostUsed, int SubscriberCount);
