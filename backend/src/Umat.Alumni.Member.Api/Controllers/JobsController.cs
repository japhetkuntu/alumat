using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
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
