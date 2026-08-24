using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface INotificationPreferenceService
{
    Task<IApiResponse<NotificationPreferenceDto>> GetPreferencesAsync(string memberId);
    Task<IApiResponse<NotificationPreferenceDto>> UpdatePreferencesAsync(UpdateNotificationPreferenceRequest request, string memberId);
}

public record UpdateNotificationPreferenceRequest(
    bool MembershipReminders,
    bool CampaignAlerts,
    bool EventReminders,
    bool JobAlerts,
    bool ClassNoteAlerts,
    bool SpotlightAlerts,
    bool SmsAlerts,
    bool WhatsAppAlerts);
