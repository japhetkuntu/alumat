using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPayoutAccountService
{
    /// <summary>
    /// Every payout account that has been set up, across institutions, year groups and groups, filtered and paged. The counts cover
    /// the institution and search chosen, whatever the state filter, so they can act as the filter itself.
    /// </summary>
    Task<IApiResponse<PayoutAccountsResponse>> GetAsync(string? institutionId, string? kind, string? state, string? search, int page, int pageSize);

    /// <summary>The full account number of one account. Logged against the staff member who asked.</summary>
    Task<IApiResponse<RevealedAccountNumber>> RevealAsync(string kind, string id, string actorId, string actorName);
}
