using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IPayoutService
{
    Task<IApiResponse<PayoutForecastResponse>> GetForecastAsync();

    /// <summary>Past payout days that fall in the last <paramref name="days"/> calendar days up to <paramref name="to"/> (default: the latest payout).</summary>
    Task<IApiResponse<PayoutHistoryResponse>> GetHistoryAsync(DateOnly? to, int days, string? account);

    /// <summary>The individual payments that made up one payout day.</summary>
    Task<IApiResponse<PayoutDayDetailDto>> GetDayAsync(DateOnly date, string? account);
}
