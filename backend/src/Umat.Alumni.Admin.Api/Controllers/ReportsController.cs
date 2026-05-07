using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Admin.Api.Controllers;

[Authorize]
public class ReportsController(
    IReportService reportService,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Job> jobRepo
) : DefaultController
{
    [HttpGet("summary")]
    [SwaggerOperation(Summary = "Get reports summary metrics")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ReportSummaryDto>))]
    public async Task<IActionResult> GetReportSummary()
    {
        var admin = User.GetAccount();
        var result = await reportService.GetReportSummaryAsync(admin);
        return result.ToActionResult();
    }

    [HttpGet("export/{entity}")]
    [SwaggerOperation(Summary = "Export entity data to CSV")]
    public async Task<IActionResult> ExportEntityCsv(string entity)
    {
        var admin = User.GetAccount();
        var result = await reportService.ExportEntityCsvAsync(entity, admin);
        if (result.Code != 200 || result.Data is null)
            return result.ToActionResult();

        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Data.Content);
        return File(bytes, "text/csv", result.Data.FileName);
    }
}
