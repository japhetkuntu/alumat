using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

/// <summary>One calendar month of paid revenue, split by source. Amounts are money actually received (successful payments only).</summary>
public record RevenueTrendMonthDto(int Year, int Month, decimal Contributions, decimal Store, decimal Services);

/// <summary>The months, plus how many payments (all time, within this admin's scope) sit in each status, for the "Payment status mix" donut.</summary>
public record RevenueTrendDto(List<RevenueTrendMonthDto> Months, Dictionary<string, int> StatusCounts);

public interface IReportService
{
    Task<IApiResponse<ReportSummaryDto>> GetReportSummaryAsync(AuthData admin);
    /// <summary>The latest N calendar months (oldest first, current month last, empty months included as zeros), worked out in the database so it is exact however many payments exist.</summary>
    Task<IApiResponse<RevenueTrendDto>> GetRevenueTrendAsync(AuthData admin, int months = 6);
}
