using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlatformBroadcastService
{
    Task<IApiResponse<int>> GetRecipientCountAsync(PlatformBroadcastFilter filter);
    Task<IApiResponse<PlatformBroadcastResult>> SendBroadcastAsync(SendPlatformBroadcastRequest request, AuthData admin);
}
