using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPayoutService
{
    /// <summary>Estimated last/next Paystack settlement, totaled across every institution and broken down per institution.</summary>
    Task<IApiResponse<PlatformPayoutForecastResponse>> GetForecastAsync();
}
