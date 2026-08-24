using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public record ReportExportResult(string FileName, string Content);

public interface IReportService
{
    Task<IApiResponse<ReportSummaryDto>> GetReportSummaryAsync(AuthData admin);
    Task<IApiResponse<ReportExportResult>> ExportEntityCsvAsync(string entity, AuthData admin);
}
