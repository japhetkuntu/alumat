using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IBatchService
{
    Task<IApiResponse<List<BatchListItem>>> GetBatchesAsync();
    Task<IApiResponse<BatchListItem>> CreateBatchAsync(CreateBatchRequest request, string createdBy);
    Task<IApiResponse<BatchListItem>> UpdateBatchAsync(string id, UpdateBatchRequest request, string updatedBy);
    Task<IApiResponse<object>> DeleteBatchAsync(string id);
}
