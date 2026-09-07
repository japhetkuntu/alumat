using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IBatchPayoutService
{
    Task<IApiResponse<List<PendingBatchPayoutItem>>> GetPendingAsync();
    Task<IApiResponse<object>> ApproveAsync(string batchId, string approvedBy, string actorName);
    Task<IApiResponse<object>> RejectAsync(string batchId, RejectBatchPayoutRequest request, string rejectedBy, string actorName);
}
