using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IAdminSpotlightService
{
    Task<IApiResponse<PgPagedResult<SpotlightDto>>> GetSpotlightsAsync(int page, int pageSize, string? status);
    Task<IApiResponse<SpotlightDto>> CreateSpotlightAsync(AdminCreateSpotlightRequest request, AuthData admin);
    Task<IApiResponse<SpotlightDto>> ApproveSpotlightAsync(string spotlightId, AuthData admin);
    Task<IApiResponse<SpotlightDto>> RejectSpotlightAsync(string spotlightId, string? reason, AuthData admin);
}

public record AdminCreateSpotlightRequest(string MemberId, string Title, string Story, string? ImageUrl);
