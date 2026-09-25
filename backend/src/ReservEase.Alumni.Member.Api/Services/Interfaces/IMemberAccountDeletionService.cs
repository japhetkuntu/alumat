using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberAccountDeletionService
{
    /// <summary>
    /// Removes a member's personal data from this institution and closes the account. Payment, order and service
    /// records are kept for the institution's books, with the member's name and contact details replaced.
    /// </summary>
    Task<IApiResponse<object>> DeleteMyAccountAsync(AuthData member, string confirmation);
}
