namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class NotificationPreference : BaseEntity
{
    public string MemberId { get; set; } = string.Empty;
    public bool MembershipReminders { get; set; } = true;
    public bool CampaignAlerts { get; set; } = true;
    public bool EventReminders { get; set; } = true;
    public bool JobAlerts { get; set; } = true;
    public bool ClassNoteAlerts { get; set; } = true;
    public bool SpotlightAlerts { get; set; } = true;
}
