using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Reports.Sdk.Models;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Reports;

/// <summary>What <see cref="ReportActivities.GenerateAsync"/> produced, handed back to the workflow to record on the job.</summary>
public record ReportFileResult(int RowCount, string FileKey, string FileName, long SizeBytes);

public class ReportActivities(
    IAlumniPgRepository<ReportJob> jobRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    IAlumniPgRepository<PlatformNotification> platformNotificationRepo,
    ReportDataBuilder dataBuilder,
    IStorageService storage,
    ITemporalClientProvider temporalProvider,
    IConfiguration configuration,
    ILogger<ReportActivities> logger)
{
    /// <summary>A job still unfinished after this long has lost its workflow (a worker outage beyond the retries); it is failed so the requester can ask again.</summary>
    public static readonly TimeSpan StuckAfter = TimeSpan.FromHours(6);

    [Activity("Report.MarkRunning")]
    public virtual async Task<bool> MarkRunningAsync(string reportJobId)
    {
        var now = DateTime.UtcNow;
        var updated = await jobRepo.ExecuteUpdateAsync(j => j.Id == reportJobId,
            s => s.SetProperty(j => j.Status, ReportJobStatuses.Running).SetProperty(j => j.StartedAt, now));
        return updated > 0;
    }

    [Activity("Report.Generate")]
    public virtual async Task<ReportFileResult> GenerateAsync(string reportJobId)
    {
        var job = await jobRepo.GetByIdAsync(reportJobId)
            ?? throw new ApplicationFailureException($"Report job {reportJobId} not found", errorType: nameof(ReportNotSupportedException), nonRetryable: true);

        var inActivity = ActivityExecutionContext.HasCurrent;
        var ct = inActivity ? ActivityExecutionContext.Current.CancellationToken : CancellationToken.None;

        try
        {
            var data = dataBuilder.Build(job);

            // Written to a self-deleting temp file, then uploaded: the file's size is known before the upload
            // starts, and neither the rows nor the finished file are ever held in memory whole.
            await using var file = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            var rows = await ReportWriters.WriteAsync(job.Format, data, file, written =>
            {
                if (inActivity) ActivityExecutionContext.Current.Heartbeat(written);
            }, ct);
            await file.FlushAsync(ct);
            var size = file.Length;
            file.Position = 0;

            var fileName = $"{Slug(job.Title)}-{DateTime.UtcNow:yyyy-MM-dd}.{job.Format}";
            // The job id in the path makes the key unique, so a retried attempt overwrites its own earlier file.
            var key = $"reports/{job.InstitutionId ?? "platform"}/{job.Id}/{fileName}";
            await storage.UploadPrivateFileAsync(file, key, ReportContentTypes.For(job.Format));

            logger.LogInformation("Generated report {ReportType} for job {ReportJobId}: {Rows} rows, {Bytes} bytes", job.ReportType, job.Id, rows, size);
            return new ReportFileResult(rows, key, fileName, size);
        }
        catch (ReportNotSupportedException e)
        {
            throw new ApplicationFailureException(e.Message, e, errorType: nameof(ReportNotSupportedException), nonRetryable: true);
        }
    }

    [Activity("Report.MarkReady")]
    public virtual async Task MarkReadyAsync(string reportJobId, ReportFileResult file)
    {
        var now = DateTime.UtcNow;
        await jobRepo.ExecuteUpdateAsync(j => j.Id == reportJobId, s => s
            .SetProperty(j => j.Status, ReportJobStatuses.Ready)
            .SetProperty(j => j.CompletedAt, now)
            .SetProperty(j => j.RowCount, file.RowCount)
            .SetProperty(j => j.FileKey, file.FileKey)
            .SetProperty(j => j.FileName, file.FileName)
            .SetProperty(j => j.FileSizeBytes, file.SizeBytes)
            .SetProperty(j => j.FailureReason, (string?)null));
    }

    [Activity("Report.MarkFailed")]
    public virtual async Task MarkFailedAsync(string reportJobId, string reason)
    {
        var now = DateTime.UtcNow;
        await jobRepo.ExecuteUpdateAsync(j => j.Id == reportJobId, s => s
            .SetProperty(j => j.Status, ReportJobStatuses.Failed)
            .SetProperty(j => j.CompletedAt, now)
            .SetProperty(j => j.FailureReason, reason));
    }

    /// <summary>An in-app notification and an email to the one person who asked, saying the report is ready (or that it failed).</summary>
    [Activity("Report.NotifyRequester")]
    public virtual async Task NotifyRequesterAsync(string reportJobId)
    {
        var job = await jobRepo.GetByIdAsync(reportJobId);
        if (job is null || job.Status is not (ReportJobStatuses.Ready or ReportJobStatuses.Failed)) return;

        var ready = job.Status == ReportJobStatuses.Ready;
        var title = ready ? $"Your {job.Title.ToLowerInvariant()} report is ready" : $"Your {job.Title.ToLowerInvariant()} report couldn't be prepared";
        var rows = job.RowCount.ToString("N0", CultureInfo.InvariantCulture);
        var body = ready
            ? $"{job.Title}: {rows} {(job.RowCount == 1 ? "row" : "rows")}. Download it from Reports."
            : $"{job.FailureReason} Nothing was produced, so there is nothing to download.";

        string portalUrl;
        object brand;
        if (job.Audience == ReportAudiences.Platform)
        {
            portalUrl = configuration["PlatformPortalUrl"]?.TrimEnd('/') ?? string.Empty;
            brand = new { };
            await platformNotificationRepo.AddAsync(new PlatformNotification
            {
                RecipientStaffId = job.RequestedById,
                Title = title,
                Body = body,
                Type = ready ? "ReportReady" : "ReportFailed",
                RelatedEntityId = job.Id,
                RelatedEntityType = "Report",
                ActionUrl = "/reports",
                CreatedBy = "system",
            });
        }
        else
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == job.InstitutionId, ignoreQueryFilters: true);
            var adminDomain = configuration["AdminPortalBaseDomain"];
            portalUrl = institution is null || string.IsNullOrWhiteSpace(adminDomain) ? string.Empty : $"https://{institution.Slug}.{adminDomain}";
            brand = new
            {
                brand_name = institution is null ? null : string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName,
                brand_color = institution?.PrimaryColorHex,
                brand_secondary_color = institution?.SecondaryColorHex,
                brand_logo = institution?.LogoUrl,
            };
            await notificationRepo.AddAsync(new Notification
            {
                InstitutionId = job.InstitutionId!,
                RecipientId = job.RequestedById,
                RecipientType = "Admin",
                Title = title,
                Body = body,
                Type = ready ? "ReportReady" : "ReportFailed",
                RelatedEntityId = job.Id,
                RelatedEntityType = "Report",
                ActionUrl = string.IsNullOrEmpty(portalUrl) ? string.Empty : $"{portalUrl}/reports",
                CreatedBy = "system",
            });
        }

        if (string.IsNullOrWhiteSpace(job.RequestedByEmail)) return;
        var variables = new Dictionary<string, object?>
        {
            ["first_name"] = job.RequestedByName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? job.RequestedByName,
            ["title"] = title,
            ["body"] = body,
            ["badge_label"] = "Reports",
            ["action_url"] = string.IsNullOrEmpty(portalUrl) ? string.Empty : $"{portalUrl}/reports",
            ["action_label"] = ready ? "Download the report" : "Open Reports",
            ["pref_label"] = "you asked for this report",
        };
        foreach (var property in brand.GetType().GetProperties())
            variables[property.Name] = property.GetValue(brand);

        // The link goes to the Reports page, never to the file: the file has no URL, and downloading it needs a signed-in session.
        await temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(
            new SendEmailRequest
            {
                To = [new EmailContact { Email = job.RequestedByEmail, Name = job.RequestedByName }],
                TemplateId = "notification",
                TemplateVariables = variables,
            },
            $"report {job.Id} {(ready ? "ready" : "failed")} to {job.RequestedByEmail}"), logger);
    }

    /// <summary>
    /// Fails jobs stuck unfinished, so the requester can ask again. Finished reports are never deleted or expired:
    /// the file and its download stay available. (The name is kept so already-scheduled runs still find it.)
    /// Returns how many jobs it touched.
    /// </summary>
    [Activity("Report.ExpireOldReports")]
    public virtual async Task<int> ExpireOldReportsAsync()
    {
        var now = DateTime.UtcNow;
        var stuckBefore = now - StuckAfter;
        return await jobRepo.ExecuteUpdateAsync(
            j => (j.Status == ReportJobStatuses.Queued || j.Status == ReportJobStatuses.Running) && j.CreatedAt < stuckBefore,
            s => s.SetProperty(j => j.Status, ReportJobStatuses.Failed)
                .SetProperty(j => j.CompletedAt, now)
                .SetProperty(j => j.FailureReason, "This report took too long and was stopped. Please request it again."));
    }

    private static string Slug(string title) => Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
}
