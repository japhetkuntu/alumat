using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public record ReportExportResult(string FileName, string Content);

/// <summary>Optional narrowing applied only to the "members" export entity — mirrors the Institution Portal's Reports page "Member Roster Filters" card.</summary>
public record MemberExportFilters(string? Status, int? GraduationYearFrom, int? GraduationYearTo, string? JobTitleContains, string? LocationContains);

public interface IReportService
{
    Task<IApiResponse<ReportSummaryDto>> GetReportSummaryAsync(AuthData admin);
    Task<IApiResponse<ReportExportResult>> ExportEntityCsvAsync(string entity, AuthData admin, MemberExportFilters? memberFilters = null);
}
