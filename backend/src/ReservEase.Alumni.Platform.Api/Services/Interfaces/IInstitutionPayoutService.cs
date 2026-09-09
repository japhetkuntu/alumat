using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IInstitutionPayoutService
{
    Task<IApiResponse<List<PendingInstitutionPayoutItem>>> GetPendingAsync();
    Task<IApiResponse<object>> ApproveAsync(string institutionId, string approvedBy, string actorName);
    Task<IApiResponse<object>> RejectAsync(string institutionId, RejectInstitutionPayoutRequest request, string rejectedBy, string actorName);
}
