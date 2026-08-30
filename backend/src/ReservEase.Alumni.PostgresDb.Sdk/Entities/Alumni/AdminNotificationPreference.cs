using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class AdminNotificationPreference : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string StaffId { get; set; } = string.Empty;
    public bool PaymentReceivedAlerts { get; set; } = true;
    public bool NewMemberRegistrationAlerts { get; set; } = true;
    public bool PendingApprovalAlerts { get; set; } = true;
    public bool SystemAlerts { get; set; } = true;
}
