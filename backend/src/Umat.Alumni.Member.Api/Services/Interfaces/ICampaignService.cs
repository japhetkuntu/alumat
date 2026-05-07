using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface ICampaignService
{
    Task<IApiResponse<PgPagedResult<CampaignDto>>> GetActiveCampaignsAsync(BaseFilter filter, string memberId);
    Task<IApiResponse<CampaignDto>> GetCampaignByIdAsync(string campaignId);
    Task<IApiResponse<CampaignDto?>> GetCurrentMembershipCampaignAsync();
}
