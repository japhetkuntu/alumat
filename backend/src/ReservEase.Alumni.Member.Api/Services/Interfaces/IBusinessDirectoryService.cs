using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IBusinessDirectoryService
{
    /// <summary>Public browse — Status == Approved and not hidden by its owner only.</summary>
    Task<IApiResponse<PgPagedResult<BusinessListingDto>>> GetListingsAsync(BusinessListingFilter filter);
    /// <summary>Same visibility filter as the list — 404 if the listing exists but isn't publicly visible.</summary>
    Task<IApiResponse<BusinessListingDto>> GetListingByIdAsync(string listingId);
    Task<IApiResponse<List<BusinessListingDto>>> GetMyListingsAsync(string memberId);
    Task<IApiResponse<BusinessListingDto>> SubmitListingAsync(SubmitBusinessListingRequest request, AuthData member);
    Task<IApiResponse<BusinessListingDto>> UpdateMyListingAsync(string listingId, UpdateMyBusinessListingRequest request, AuthData member);
    Task<IApiResponse<BusinessListingDto>> HideListingAsync(string listingId, AuthData member);
    Task<IApiResponse<BusinessListingDto>> UnhideListingAsync(string listingId, AuthData member);
    Task<IApiResponse<object>> DeleteMyListingAsync(string listingId, AuthData member);
}
