using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class NotificationPreferenceService(
    IAlumniPgRepository<NotificationPreference> prefRepo,
    ILogger<NotificationPreferenceService> logger) : INotificationPreferenceService
{
    public async Task<IApiResponse<NotificationPreferenceDto>> GetPreferencesAsync(string memberId)
    {
        try
        {
            var pref = await prefRepo.GetOneAsync(p => p.MemberId == memberId);
            if (pref is null)
            {
                pref = new NotificationPreference
                {
                    MemberId = memberId,
                    CreatedBy = memberId,
                };
                await prefRepo.AddAsync(pref);
            }

            return pref.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving notification preferences for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<NotificationPreferenceDto>("Failed to retrieve preferences");
        }
    }

    public async Task<IApiResponse<NotificationPreferenceDto>> UpdatePreferencesAsync(
        UpdateNotificationPreferenceRequest request, string memberId)
    {
        try
        {
            var pref = await prefRepo.GetOneAsync(p => p.MemberId == memberId);
            if (pref is null)
            {
                pref = new NotificationPreference
                {
                    MemberId = memberId,
                    CreatedBy = memberId,
                };
                await prefRepo.AddAsync(pref);
            }

            pref.MembershipReminders = request.MembershipReminders;
            pref.CampaignAlerts = request.CampaignAlerts;
            pref.EventReminders = request.EventReminders;
            pref.JobAlerts = request.JobAlerts;
            pref.ClassNoteAlerts = request.ClassNoteAlerts;
            pref.SpotlightAlerts = request.SpotlightAlerts;
            pref.SmsAlerts = request.SmsAlerts;
            pref.WhatsAppAlerts = request.WhatsAppAlerts;
            pref.UpdatedBy = memberId;

            await prefRepo.UpdateAsync(pref);
            return pref.ToDto().ToOkApiResponse("Preferences updated.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating notification preferences for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<NotificationPreferenceDto>("Failed to update preferences");
        }
    }
}
