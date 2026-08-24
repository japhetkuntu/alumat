using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface ICampaignService
{
    Task<IApiResponse<PgPagedResult<CampaignDto>>> GetActiveCampaignsAsync(BaseFilter filter, string memberId, string? communityId = null);
    Task<IApiResponse<CampaignDto>> GetCampaignByIdAsync(string campaignId, string? memberId = null);
    Task<IApiResponse<CampaignDto?>> GetCurrentMembershipCampaignAsync();
}
