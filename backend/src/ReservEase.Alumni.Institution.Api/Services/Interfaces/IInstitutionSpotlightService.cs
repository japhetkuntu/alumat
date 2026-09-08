using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IInstitutionSpotlightService
{
    Task<IApiResponse<PgPagedResult<SpotlightDto>>> GetSpotlightsAsync(int page, int pageSize, string? status);
    Task<IApiResponse<SpotlightDto>> CreateSpotlightAsync(AdminCreateSpotlightRequest request, AuthData admin);
    Task<IApiResponse<SpotlightDto>> UpdateSpotlightAsync(string spotlightId, UpdateSpotlightRequest request, AuthData admin);
    Task<IApiResponse<SpotlightDto>> ApproveSpotlightAsync(string spotlightId, AuthData admin);
    Task<IApiResponse<SpotlightDto>> RejectSpotlightAsync(string spotlightId, string? reason, AuthData admin);
}

public record AdminCreateSpotlightRequest(string MemberId, string Title, string Story, string? ImageUrl);
public record UpdateSpotlightRequest(string Title, string Story, string? ImageUrl);
