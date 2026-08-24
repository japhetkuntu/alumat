using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class NotificationPreference : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;
    public bool MembershipReminders { get; set; } = true;
    public bool CampaignAlerts { get; set; } = true;
    public bool EventReminders { get; set; } = true;
    public bool JobAlerts { get; set; } = true;
    public bool ClassNoteAlerts { get; set; } = true;
    public bool SpotlightAlerts { get; set; } = true;

    // Opt-in (default false), unlike the in-app/email alerts above — SMS and
    // WhatsApp cost money per message and are more intrusive, so a member has
    // to explicitly turn these on rather than explicitly turn them off.
    public bool SmsAlerts { get; set; } = false;
    public bool WhatsAppAlerts { get; set; } = false;
}
