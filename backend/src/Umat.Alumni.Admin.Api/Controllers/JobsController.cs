using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// Manage job postings.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
public class JobsController(IJobService jobService) : DefaultController
{
    /// <summary>
    /// Get a paginated list of job postings.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List jobs", Description = "Filter by status (Active, Expired, Closed).")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<JobDto>>))]
    public async Task<IActionResult> GetJobs([FromQuery] JobFilter filter)
    {
        var admin = User.GetAccount();
        var result = await jobService.GetJobsAsync(filter, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a single job posting by ID.
    /// </summary>
    [HttpGet("{jobId}")]
    [SwaggerOperation(Summary = "Get job by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<JobDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetJob(string jobId)
    {
        var admin = User.GetAccount();
        var result = await jobService.GetJobByIdAsync(jobId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Close a job posting.
    /// </summary>
    [HttpPut("{jobId}/close")]
    [SwaggerOperation(Summary = "Close job")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CloseJob(string jobId)
    {
        var admin = User.GetAccount();
        var result = await jobService.CloseJobAsync(jobId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new job posting.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create job")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<JobDto>))]
    public async Task<IActionResult> CreateJob([FromForm] CreateJobRequest request)
    {
        var admin = User.GetAccount();
        var result = await jobService.CreateJobAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a job posting.
    /// </summary>
    [HttpDelete("{jobId}")]
    [SwaggerOperation(Summary = "Delete job")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteJob(string jobId)
    {
        var admin = User.GetAccount();
        var result = await jobService.DeleteJobAsync(jobId, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Update a job posting.
    /// </summary>
    [HttpPut("{jobId}")]
    [SwaggerOperation(Summary = "Update job")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<JobDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateJob(string jobId, [FromForm] UpdateJobRequest request)
    {
        request.JobId = jobId;
        var admin = User.GetAccount();
        var result = await jobService.UpdateJobAsync(request, admin);
        return result.ToActionResult();
    }
}
