using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface INotificationPreferenceService
{
    Task<IApiResponse<AdminNotificationPreferenceDto>> GetPreferencesAsync(string staffId);
    Task<IApiResponse<AdminNotificationPreferenceDto>> UpdatePreferencesAsync(UpdateAdminNotificationPreferenceRequest request, string staffId);
}

public record UpdateAdminNotificationPreferenceRequest(
    bool PaymentReceivedAlerts,
    bool NewMemberRegistrationAlerts,
    bool PendingApprovalAlerts,
    bool SystemAlerts);
