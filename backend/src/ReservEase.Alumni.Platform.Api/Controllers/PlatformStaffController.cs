using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Manage platform staff accounts. SuperAdmin only.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/staff")]
public class PlatformStaffController(IPlatformStaffService staffService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List platform staff")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<PlatformStaffResponse>>))]
    public async Task<IActionResult> GetStaff([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var result = await staffService.GetStaffAsync(page, pageSize, search);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Invite a platform staff member")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<PlatformStaffResponse>))]
    public async Task<IActionResult> CreateStaff([FromBody] CreatePlatformStaffRequest request)
    {
        var acct = User.GetAccount();
        var result = await staffService.CreateAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpPatch("{id}")]
    [SwaggerOperation(Summary = "Update a platform staff member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformStaffResponse>))]
    public async Task<IActionResult> UpdateStaff(string id, [FromBody] UpdatePlatformStaffRequest request)
    {
        var acct = User.GetAccount();
        var result = await staffService.UpdateAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
