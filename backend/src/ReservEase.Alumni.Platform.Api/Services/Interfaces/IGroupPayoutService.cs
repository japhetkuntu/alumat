using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IGroupPayoutService
{
    Task<IApiResponse<List<PendingGroupPayoutItem>>> GetPendingAsync();
    Task<IApiResponse<object>> ApproveAsync(string communityId, string approvedBy, string actorName);
    Task<IApiResponse<object>> RejectAsync(string communityId, RejectGroupPayoutRequest request, string rejectedBy, string actorName);
}
