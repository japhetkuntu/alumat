using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage institution staff accounts (SuperAdmin only).
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/staff")]
public class InstitutionStaffController(IInstitutionStaffService staffService) : DefaultController
{
    /// <summary>
    /// List institution staff.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List institution staff")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<InstitutionStaffListItem>>))]
    public async Task<IActionResult> GetInstitutionStaff([FromQuery] InstitutionStaffFilter filter)
    {
        var result = await staffService.GetInstitutionStaffAsync(filter);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new institution staff member.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create institution staff")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<InstitutionStaffListItem>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateInstitutionStaff([FromBody] CreateInstitutionStaffRequest request)
    {
        var actor = User.GetAccount();
        var result = await staffService.CreateInstitutionStaffAsync(request, actor);
        return result.ToActionResult();
    }

    /// <summary>
    /// Update an existing institution staff member.
    /// </summary>
    [HttpPut("{staffId}")]
    [SwaggerOperation(Summary = "Update institution staff")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionStaffListItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateInstitutionStaff(string staffId, [FromBody] UpdateInstitutionStaffRequest request)
    {
        var actor = User.GetAccount();
        var result = await staffService.UpdateInstitutionStaffAsync(staffId, request, actor);
        return result.ToActionResult();
    }
}
