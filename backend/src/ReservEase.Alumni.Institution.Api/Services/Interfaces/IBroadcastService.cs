using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IBroadcastService
{
    Task<IApiResponse<int>> GetRecipientCountAsync(BroadcastFilter filter, AuthData admin);
    Task<IApiResponse<BroadcastResult>> SendBroadcastAsync(SendBroadcastRequest request, AuthData admin);
}
