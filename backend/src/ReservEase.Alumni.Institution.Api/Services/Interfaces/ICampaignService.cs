using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

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
    Task<IApiResponse<List<CampaignUpdateDto>>> GetUpdatesAsync(string campaignId);
    Task<IApiResponse<CampaignUpdateDto>> CreateUpdateAsync(string campaignId, CreateCampaignUpdateRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteUpdateAsync(string campaignId, string updateId);
}

public interface IContributionService
{
    Task<IApiResponse<PgPagedResult<ContributionDto>>> GetContributionsAsync(ContributionInstitutionStaffFilter filter, AuthData admin);
    Task<IApiResponse<ContributionDto>> RecordManualContributionAsync(RecordManualContributionRequest request, AuthData admin);
    Task<IApiResponse<object>> ConfirmContributionAsync(string contributionId, AuthData admin);
    Task<IApiResponse<object>> RejectContributionAsync(string contributionId, string? reason, AuthData admin);
}
