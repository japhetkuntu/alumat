using System.Globalization;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Reports.Sdk.Models;
using ReservEase.Alumni.Reports.Sdk.Workflows;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;

namespace ReservEase.Alumni.Reports.Sdk.Services;

public class ReportJobService(
    IAlumniPgRepository<ReportJob> jobRepo,
    ITemporalClientProvider temporalProvider,
    IStorageService storage,
    ILogger<ReportJobService> logger) : IReportJobService
{
    /// <summary>
    /// Reports one person may have queued or running at once. Generation is the expensive part of
    /// this feature; without a cap, one impatient series of clicks is a queue full of the same report.
    /// </summary>
    public const int MaxInFlightPerRequester = 3;

    private static bool MayRun(ReportDefinition definition, ReportRequester requester) =>
        definition.Audience == requester.Audience
        && (definition.Feature is null || !requester.DisabledFeatures.Contains(definition.Feature))
        && !(definition.UnscopedOnly && requester.IsScoped)
        && (definition.Roles.Count == 0 || definition.Roles.Contains(requester.Role));

    public IReadOnlyList<ReportDefinitionDto> GetAvailable(ReportRequester requester) =>
        ReportCatalog.All.Where(d => MayRun(d, requester))
            .Select(d => new ReportDefinitionDto(d.Key, d.Title, d.Question, d.Parameters, d.StatusOptions))
            .ToList();

    public async Task<IApiResponse<ReportJobDto>> RequestAsync(ReportRequester requester, RequestReportRequest request)
    {
        try
        {
            var definition = ReportCatalog.Find(request.ReportType);
            // The same answer whether the report doesn't exist or isn't theirs to run — no hint about which.
            if (definition is null || !MayRun(definition, requester))
                return ApiResponseExtensions.ToNotFoundApiResponse<ReportJobDto>("That report isn't available.");

            var format = string.IsNullOrWhiteSpace(request.Format) ? ReportFormats.Xlsx : request.Format.Trim().ToLowerInvariant();
            if (!ReportFormats.All.Contains(format))
                return ApiResponseExtensions.ToBadRequestApiResponse<ReportJobDto>("Choose Excel or CSV as the format.");

            var (parameters, problem) = CleanParameters(definition, request.Parameters);
            if (problem is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<ReportJobDto>(problem);

            var inFlight = await jobRepo.CountAsync(j => j.RequestedById == requester.Id
                && (j.Status == ReportJobStatuses.Queued || j.Status == ReportJobStatuses.Running));
            if (inFlight >= MaxInFlightPerRequester)
                return ApiResponseExtensions.ToConflictApiResponse<ReportJobDto>(
                    $"You already have {inFlight} reports being prepared. Wait for one to finish before asking for another.");

            if (!temporalProvider.IsAvailable)
                return ApiResponseExtensions.ToServerErrorApiResponse<ReportJobDto>("Reports can't be prepared right now. Please try again in a few minutes.");

            var job = new ReportJob
            {
                Audience = requester.Audience,
                InstitutionId = requester.InstitutionId,
                RequestedById = requester.Id,
                RequestedByName = requester.Name,
                RequestedByEmail = requester.Email,
                RequesterIsScoped = requester.IsScoped,
                ScopeYearGroups = [.. requester.YearGroups],
                ScopeCommunityIds = [.. requester.CommunityIds],
                ReportType = definition.Key,
                Title = definition.Title,
                Format = format,
                Parameters = parameters,
                CreatedBy = requester.Id,
            };
            await jobRepo.AddAsync(job);

            try
            {
                await temporalProvider.Client!.StartOrAttachAsync<IGenerateReportWorkflow>(
                    wf => wf.RunAsync(job.Id), ReportWorkflowId.For(job.Id), ReportTaskQueues.Generation);
            }
            catch (Exception e)
            {
                // The row is already saved; without a workflow behind it, it would sit at "Queued" forever.
                logger.LogError(e, "Could not start report workflow for job {ReportJobId}", job.Id);
                job.Status = ReportJobStatuses.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.FailureReason = "The report could not be started. Please try again.";
                await jobRepo.UpdateAsync(job);
                return ApiResponseExtensions.ToServerErrorApiResponse<ReportJobDto>("Reports can't be prepared right now. Please try again in a few minutes.");
            }

            return ReportJobDto.From(job).ToCreatedApiResponse("We're preparing your report. You'll be notified when it's ready to download.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error requesting report {ReportType} for {RequesterId}", request.ReportType, requester.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ReportJobDto>("Failed to request the report");
        }
    }

    public async Task<IApiResponse<PgPagedResult<ReportJobDto>>> ListAsync(ReportRequester requester, int page, int pageSize)
    {
        try
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            var result = await jobRepo.GetPagedAsync(Math.Max(page, 1), pageSize, "CreatedAt", "desc",
                j => j.RequestedById == requester.Id && j.Audience == requester.Audience && j.InstitutionId == requester.InstitutionId);
            return new PgPagedResult<ReportJobDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(ReportJobDto.From).ToList(),
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing reports for {RequesterId}", requester.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ReportJobDto>>("Failed to retrieve your reports");
        }
    }

    public async Task<ReportDownload?> OpenDownloadAsync(ReportRequester requester, string reportJobId)
    {
        var job = await jobRepo.GetOneAsync(j => j.Id == reportJobId
            && j.RequestedById == requester.Id && j.Audience == requester.Audience && j.InstitutionId == requester.InstitutionId);
        // Finished reports do not expire: any report that is ready and still has its file can be downloaded.
        if (job is null || job.Status != ReportJobStatuses.Ready || string.IsNullOrEmpty(job.FileKey))
            return null;
        Stream content;
        try
        {
            content = await storage.OpenPrivateFileAsync(job.FileKey);
        }
        catch (Amazon.S3.AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound || e.ErrorCode is "NoSuchKey" or "NoSuchBucket")
        {
            // Marked ready, but the file is nowhere this process can find it (the lookup already tried the other places a worker
            // may have written it): it was removed, or the worker and this API are using different buckets. The person is told
            // plainly to ask again, and the log says exactly which file was missing.
            logger.LogWarning(e, "Report job {ReportJobId} is ready but its file {FileKey} was not found in storage (S3 status {Status}, code {ErrorCode})", job.Id, job.FileKey, (int)e.StatusCode, e.ErrorCode ?? "none");
            throw new ReportFileMissingException("The report's file could not be found in storage.", e);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not read the file {FileKey} for report job {ReportJobId} from storage", job.FileKey, job.Id);
            throw new ReportFileUnavailableException("The report file couldn't be read from storage right now.", e);
        }

        return new ReportDownload(content, job.FileName ?? $"report.{job.Format}", ReportContentTypes.For(job.Format));
    }

    /// <summary>
    /// Keeps only the filters this report understands, trimmed and checked. Anything the report
    /// doesn't list is dropped rather than rejected, so a client can send one filter form to every report.
    /// </summary>
    private static (Dictionary<string, string> Parameters, string? Problem) CleanParameters(ReportDefinition definition, Dictionary<string, string>? raw)
    {
        var clean = new Dictionary<string, string>();
        foreach (var (key, rawValue) in raw ?? [])
        {
            var value = rawValue?.Trim();
            if (string.IsNullOrEmpty(value) || !definition.Parameters.Contains(key)) continue;

            switch (key)
            {
                case ReportParameters.From or ReportParameters.To:
                    if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        return (clean, "Dates must be given as year-month-day, for example 2026-01-31.");
                    break;
                case ReportParameters.YearFrom or ReportParameters.YearTo or ReportParameters.Year:
                    if (!int.TryParse(value, out var year) || year is < 1900 or > 2200)
                        return (clean, "Enter a year as four digits, for example 2014.");
                    break;
                case ReportParameters.Status:
                    if (!definition.StatusOptions.Contains(value))
                        return (clean, $"Status must be one of: {string.Join(", ", definition.StatusOptions)}.");
                    break;
                default:
                    if (value.Length > 100)
                        return (clean, "That filter is too long. Keep it under 100 characters.");
                    break;
            }
            clean[key] = value;
        }

        if (clean.TryGetValue(ReportParameters.From, out var from) && clean.TryGetValue(ReportParameters.To, out var to)
            && string.CompareOrdinal(from, to) > 0)
            return (clean, "The start date is after the end date.");
        if (clean.TryGetValue(ReportParameters.YearFrom, out var yearFrom) && clean.TryGetValue(ReportParameters.YearTo, out var yearTo)
            && int.Parse(yearFrom) > int.Parse(yearTo))
            return (clean, "The first year is after the last year.");

        return (clean, null);
    }
}
