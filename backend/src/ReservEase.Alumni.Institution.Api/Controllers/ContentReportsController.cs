using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>Reports members have made about content in this institution's community.</summary>
[Authorize(Roles = "SuperAdmin")]
public class ContentReportsController(
    IAlumniPgRepository<ContentReport> reportRepo,
    IInstitutionAuditLogService auditLog) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List content reports")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ContentReportDto>>))]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
    {
        var s = string.IsNullOrWhiteSpace(status) ? null : status;
        var paged = await reportRepo.GetPagedAsync(page, pageSize, "CreatedAt", "desc",
            r => s == null || r.Status == s);

        var result = new PgPagedResult<ContentReportDto>
        {
            PageIndex = paged.PageIndex,
            PageSize = paged.PageSize,
            Count = paged.Count,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            LowerBoundSize = paged.LowerBoundSize,
            UpperBoundSize = paged.UpperBoundSize,
            Results = paged.Results.Select(ToDto).ToList(),
        };
        return result.ToOkApiResponse().ToActionResult();
    }

    [HttpGet("open-count")]
    [SwaggerOperation(Summary = "How many reports are waiting for review")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    public async Task<IActionResult> OpenCount() =>
        (await reportRepo.CountAsync(r => r.Status == ContentReportStatuses.Open)).ToOkApiResponse().ToActionResult();

    [HttpPatch("{id}")]
    [SwaggerOperation(Summary = "Review a report", Description = "Mark a report as action taken, dismissed, or reopen it. Recorded in the audit log.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ContentReportDto>))]
    public async Task<IActionResult> Review(string id, [FromBody] ReviewContentReportRequest request)
    {
        var admin = User.GetAccount();
        if (!ContentReportStatuses.All.Contains(request.Status))
            return ApiResponseExtensions.ToBadRequestApiResponse<ContentReportDto>("Unknown status.").ToActionResult();

        var report = await reportRepo.GetByIdAsync(id);
        if (report is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<ContentReportDto>("Report not found.").ToActionResult();

        report.Status = request.Status;
        report.ResolutionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var reopened = request.Status == ContentReportStatuses.Open;
        report.ReviewedById = reopened ? null : admin.Id;
        report.ReviewedByName = reopened ? null : admin.Name;
        report.ReviewedAt = reopened ? null : DateTime.UtcNow;
        report.UpdatedBy = admin.Id;
        await reportRepo.UpdateAsync(report);

        await auditLog.LogAsync(admin, $"Content Report {request.Status}", $"{report.EntityType}: {report.EntityTitle}");
        return ToDto(report).ToOkApiResponse().ToActionResult();
    }

    private static ContentReportDto ToDto(ContentReport r) => new(
        r.Id, r.EntityType, r.EntityId, r.EntityTitle, r.Reason, r.Details, r.ReporterName, r.Status, r.CreatedAt,
        r.ReviewedByName, r.ReviewedAt, r.ResolutionNote);
}
