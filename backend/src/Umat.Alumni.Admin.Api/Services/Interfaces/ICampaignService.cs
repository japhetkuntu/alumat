using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface ICampaignService
{
    Task<IApiResponse<PgPagedResult<CampaignDto>>> GetCampaignsAsync(CampaignFilter filter, AuthData admin);
    Task<IApiResponse<CampaignDto>> GetCampaignByIdAsync(string campaignId, AuthData admin);
    Task<IApiResponse<CampaignDto>> CreateCampaignAsync(CreateCampaignRequest request, AuthData admin);
    Task<IApiResponse<CampaignDto>> UpdateCampaignAsync(UpdateCampaignRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteCampaignAsync(string campaignId, AuthData admin);
    Task<IApiResponse<CampaignDto>> ArchiveCampaignAsync(string campaignId, AuthData admin);
    Task<IApiResponse<CampaignDto>> UnarchiveCampaignAsync(string campaignId, AuthData admin);
    Task<IApiResponse<CampaignDto>> ActivateCampaignAsync(string campaignId, AuthData admin);
    Task<IApiResponse<PaystackDisbursementSummaryDto>> GetCampaignPaystackSummaryAsync(string campaignId, AuthData admin);
    Task<IApiResponse<object>> MarkCampaignPaystackDisbursedAsync(string campaignId, AuthData admin);
}

public interface IContributionService
{
    Task<IApiResponse<PgPagedResult<ContributionDto>>> GetContributionsAsync(ContributionAdminFilter filter, AuthData admin);
    Task<IApiResponse<ContributionDto>> RecordManualContributionAsync(RecordManualContributionRequest request, AuthData admin);
    Task<IApiResponse<object>> ConfirmContributionAsync(string contributionId, AuthData admin);
    Task<IApiResponse<object>> RejectContributionAsync(string contributionId, string? reason, AuthData admin);
}
