using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class NotificationPreferenceService(
    IAlumniPgRepository<AdminNotificationPreference> prefRepo,
    ILogger<NotificationPreferenceService> logger) : INotificationPreferenceService
{
    public async Task<IApiResponse<AdminNotificationPreferenceDto>> GetPreferencesAsync(string staffId)
    {
        try
        {
            var pref = await prefRepo.GetOneAsync(p => p.StaffId == staffId);
            if (pref is null)
            {
                pref = new AdminNotificationPreference
                {
                    StaffId = staffId,
                    CreatedBy = staffId,
                };
                await prefRepo.AddAsync(pref);
            }

            return pref.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving admin notification preferences for staff {StaffId}", staffId);
            return ApiResponseExtensions.ToServerErrorApiResponse<AdminNotificationPreferenceDto>("Failed to retrieve preferences");
        }
    }

    public async Task<IApiResponse<AdminNotificationPreferenceDto>> UpdatePreferencesAsync(
        UpdateAdminNotificationPreferenceRequest request, string staffId)
    {
        try
        {
            var pref = await prefRepo.GetOneAsync(p => p.StaffId == staffId);
            if (pref is null)
            {
                pref = new AdminNotificationPreference
                {
                    StaffId = staffId,
                    CreatedBy = staffId,
                };
                await prefRepo.AddAsync(pref);
            }

            pref.PaymentReceivedAlerts = request.PaymentReceivedAlerts;
            pref.NewMemberRegistrationAlerts = request.NewMemberRegistrationAlerts;
            pref.PendingApprovalAlerts = request.PendingApprovalAlerts;
            pref.SystemAlerts = request.SystemAlerts;
            pref.UpdatedBy = staffId;

            await prefRepo.UpdateAsync(pref);
            return pref.ToDto().ToOkApiResponse("Preferences updated.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating admin notification preferences for staff {StaffId}", staffId);
            return ApiResponseExtensions.ToServerErrorApiResponse<AdminNotificationPreferenceDto>("Failed to update preferences");
        }
    }
}
