using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
[RequireFeature(InstitutionFeatures.Jobs)]
public class JobsController(IMemberJobService jobService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List jobs", Description = "Get a paginated list of job postings, optionally filtered by type")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<JobDto>>))]
    public async Task<IActionResult> GetJobs([FromQuery] JobFilter filter)
    {
        var result = await jobService.GetJobsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("{jobId}")]
    [SwaggerOperation(Summary = "Get job by ID", Description = "Get full details of a single job posting")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<JobDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetJob(string jobId)
    {
        var result = await jobService.GetJobByIdAsync(jobId);
        return result.ToActionResult();
    }
}
