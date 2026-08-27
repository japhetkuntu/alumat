using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IInstitutionManagementService
{
    Task<IApiResponse<PgPagedResult<InstitutionListItemResponse>>> GetInstitutionsAsync(
        int page, int pageSize, string? search, string? status);
    Task<IApiResponse<InstitutionDetailResponse>> GetInstitutionAsync(string id);
    Task<IApiResponse<InstitutionDetailResponse>> CreateInstitutionAsync(CreateInstitutionRequest request, string createdBy, string actorName);
    Task<IApiResponse<InstitutionDetailResponse>> UpdateStatusAsync(string id, UpdateInstitutionStatusRequest request, string updatedBy, string actorName);
    Task<IApiResponse<InstitutionDetailResponse>> UpdateBrandingAsync(string id, UpdateInstitutionBrandingRequest request, string updatedBy, string actorName);
    Task<IApiResponse<InstitutionDetailResponse>> UpdateFeaturesAsync(string id, UpdateInstitutionFeaturesRequest request, string updatedBy, string actorName);
    Task<IApiResponse<InstitutionDetailResponse>> UpdateLandingContentAsync(string id, UpdateInstitutionLandingContentRequest request, string updatedBy, string actorName);
    Task<IApiResponse<InstitutionDetailResponse>> UpdatePaymentsAsync(string id, UpdateInstitutionPaymentsRequest request, string updatedBy, string actorName);
    Task<IApiResponse<InstitutionRevenueResponse>> GetRevenueAsync(string id);
    Task<IApiResponse<SlugAvailabilityResponse>> CheckSlugAsync(string slug);
    BaseDomainsResponse GetBaseDomains();
    Task<IApiResponse<PlatformDashboardSummary>> GetDashboardSummaryAsync();
    Task<IApiResponse<List<InstitutionStaffDto>>> GetInstitutionStaffAsync(string institutionId);
    Task<IApiResponse<InstitutionStaffDto>> InviteInstitutionStaffAsync(string institutionId, InviteInstitutionStaffRequest request, string createdBy, string actorName);
    Task<IApiResponse<InstitutionStaffDto>> SetInstitutionStaffDisabledAsync(string institutionId, string staffId, bool isDisabled, string updatedBy, string actorName);
}
