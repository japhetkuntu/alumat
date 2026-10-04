using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.Reports.Sdk.Models;

namespace ReservEase.Alumni.Reports.Sdk.Services;

public interface IReportJobService
{
    /// <summary>The reports this person may run: right audience, feature switched on, and within their role or scope.</summary>
    IReadOnlyList<ReportDefinitionDto> GetAvailable(ReportRequester requester);

    /// <summary>Records the request and hands it to the worker. Returns as soon as it is queued.</summary>
    Task<IApiResponse<ReportJobDto>> RequestAsync(ReportRequester requester, RequestReportRequest request);

    /// <summary>The requester's own reports, newest first.</summary>
    Task<IApiResponse<PgPagedResult<ReportJobDto>>> ListAsync(ReportRequester requester, int page, int pageSize);

    /// <summary>Opens the requester's own finished report. Null when there is nothing they may download: not theirs or not ready. Finished reports do not expire.</summary>
    Task<ReportDownload?> OpenDownloadAsync(ReportRequester requester, string reportJobId);
}
