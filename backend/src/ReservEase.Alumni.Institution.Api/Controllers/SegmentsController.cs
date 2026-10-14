using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>Saved audiences, reusable for messages and reports. Full administrators only.</summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/segments")]
public class SegmentsController(ISegmentService segments) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List saved segments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<SegmentDto>>))]
    public async Task<IActionResult> List() => (await segments.ListAsync()).ToActionResult();

    [HttpPost]
    [SwaggerOperation(Summary = "Save a segment")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<SegmentDto>))]
    public async Task<IActionResult> Create([FromBody] SaveSegmentRequest request)
        => (await segments.CreateAsync(request, User.GetAccount())).ToActionResult();

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Update a segment")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SegmentDto>))]
    public async Task<IActionResult> Update(string id, [FromBody] SaveSegmentRequest request)
        => (await segments.UpdateAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete a segment")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Delete(string id)
        => (await segments.DeleteAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("preview")]
    [SwaggerOperation(Summary = "Count the members a rule describes right now", Description = "Works on an unsaved rule so it can be checked while it is being built.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SegmentPreview>))]
    public async Task<IActionResult> Preview([FromBody] SegmentCriteria criteria)
        => (await segments.PreviewAsync(criteria, User.GetAccount())).ToActionResult();
}
