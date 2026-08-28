using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// An institution-staff-posted update on a campaign's progress — "foundation
/// poured", "here's the finished classroom" — so givers see what their money
/// actually did instead of the campaign going quiet after they pay. Posted
/// any time during (or after) the campaign, not just at completion.
/// </summary>
public class CampaignUpdate : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? PostedByStaffId { get; set; }
    public string? PostedByName { get; set; }
}
