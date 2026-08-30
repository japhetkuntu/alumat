using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IBusinessDirectoryService
{
    Task<IApiResponse<PgPagedResult<BusinessListingDto>>> GetListingsAsync(BusinessListingFilter filter);
    Task<IApiResponse<BusinessListingDto>> GetListingByIdAsync(string listingId);
    Task<IApiResponse<BusinessListingDto>> CreateListingAsync(CreateBusinessListingRequest request, AuthData admin);
    Task<IApiResponse<BusinessListingDto>> ApproveListingAsync(string listingId, AuthData admin);
    Task<IApiResponse<BusinessListingDto>> RejectListingAsync(string listingId, string? adminNotes, AuthData admin);
    Task<IApiResponse<BusinessListingDto>> ApproveEditAsync(string listingId, AuthData admin);
    Task<IApiResponse<BusinessListingDto>> RejectEditAsync(string listingId, string? adminNotes, AuthData admin);
    Task<IApiResponse<BusinessListingDto>> BlacklistListingAsync(string listingId, AuthData admin);
    Task<IApiResponse<BusinessListingDto>> UnblacklistListingAsync(string listingId, AuthData admin);
    Task<IApiResponse<BusinessListingDto>> UpdateListingAsync(string listingId, UpdateBusinessListingRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteListingAsync(string listingId);
}
