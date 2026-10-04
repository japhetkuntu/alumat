using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Reports.Sdk.Models;

/// <summary>
/// Who is asking, as far as reports care. Each API builds this from its own signed-in user, so the
/// shared service never needs to know how institution staff and platform staff differ.
/// </summary>
/// <param name="IsScoped">An institution ScopedAdmin: sees only their year groups and communities.</param>
/// <param name="DisabledFeatures">The institution's switched-off features. Empty for platform staff.</param>
public record ReportRequester(
    string Audience, string? InstitutionId, string Id, string Name, string Email, string Role,
    bool IsScoped, IReadOnlyList<int> YearGroups, IReadOnlyList<string> CommunityIds,
    IReadOnlyCollection<string> DisabledFeatures);

public class RequestReportRequest
{
    public string ReportType { get; set; } = string.Empty;
    /// <summary>"xlsx" (default) or "csv".</summary>
    public string? Format { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

public record ReportDefinitionDto(string Key, string Title, string Question, IReadOnlyList<string> Parameters, IReadOnlyList<string> StatusOptions);

public class ReportJobDto
{
    public string Id { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int RowCount { get; set; }
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? FailureReason { get; set; }

    public static ReportJobDto From(ReportJob job) => new()
    {
        Id = job.Id, ReportType = job.ReportType, Title = job.Title, Format = job.Format, Parameters = job.Parameters,
        Status = job.Status, RequestedAt = job.CreatedAt, CompletedAt = job.CompletedAt, ExpiresAt = job.ExpiresAt,
        RowCount = job.RowCount, FileName = job.FileName, FileSizeBytes = job.FileSizeBytes, FailureReason = job.FailureReason,
    };
}

/// <summary>A finished report opened for download. The caller disposes <paramref name="Content"/>.</summary>
public record ReportDownload(Stream Content, string FileName, string ContentType);

public static class ReportContentTypes
{
    public static string For(string format) => format == ReportFormats.Csv
        ? "text/csv"
        : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
