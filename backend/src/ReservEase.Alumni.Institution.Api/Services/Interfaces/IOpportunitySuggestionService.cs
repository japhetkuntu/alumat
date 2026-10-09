using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IOpportunitySuggestionService
{
    /// <summary>Makes a member's suggestion live, alerts members as a new posting would, and tells the person who suggested it.</summary>
    Task<IApiResponse<JobDto>> ApproveAsync(string jobId, AuthData admin);
    /// <summary>Declines a suggestion (it is closed, not deleted) and tells the person who suggested it.</summary>
    Task<IApiResponse<object>> DeclineAsync(string jobId, AuthData admin);
    /// <summary>Fills in who suggested each still-pending opportunity in a list.</summary>
    Task NameSuggestersAsync(IEnumerable<JobDto> jobs);
}
