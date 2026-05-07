using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface ISpotlightService
{
    Task<IApiResponse<PgPagedResult<SpotlightDto>>> GetApprovedSpotlightsAsync(int page, int pageSize);
    Task<IApiResponse<SpotlightDto>> GetSpotlightByIdAsync(string spotlightId);
    Task<IApiResponse<SpotlightDto>> SubmitSpotlightAsync(SubmitSpotlightRequest request, AuthData member);
    Task<IApiResponse<List<SpotlightDto>>> GetMySpotlightsAsync(string memberId);
}

public record SubmitSpotlightRequest(string Title, string Story, string? ImageUrl);
