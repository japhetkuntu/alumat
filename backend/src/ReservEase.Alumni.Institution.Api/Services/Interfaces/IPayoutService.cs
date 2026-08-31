using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IPayoutService
{
    Task<IApiResponse<PayoutForecastResponse>> GetForecastAsync();
}
