using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public record ReportExportResult(string FileName, string Content);

public interface IReportService
{
    Task<IApiResponse<ReportSummaryDto>> GetReportSummaryAsync(AuthData admin);
    Task<IApiResponse<ReportExportResult>> ExportEntityCsvAsync(string entity, AuthData admin);
}
