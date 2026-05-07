using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// Manage admin user accounts (SuperAdmin only).
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/admins")]
public class AdminManagementController(IAdminManagementService adminService) : DefaultController
{
    /// <summary>
    /// List admin users.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List admins")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<AdminListItem>>))]
    public async Task<IActionResult> GetAdmins([FromQuery] AdminFilter filter)
    {
        var result = await adminService.GetAdminsAsync(filter);
        return result.ToActionResult();
    }

    /// <summary>
    /// Create a new admin user.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create admin")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<AdminListItem>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest request)
    {
        var admin = User.GetAccount();
        var result = await adminService.CreateAdminAsync(request, admin);
        return result.ToActionResult();
    }

    /// <summary>
    /// Update an existing admin user.
    /// </summary>
    [HttpPut("{adminId}")]
    [SwaggerOperation(Summary = "Update admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AdminListItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateAdmin(string adminId, [FromBody] UpdateAdminRequest request)
    {
        var admin = User.GetAccount();
        var result = await adminService.UpdateAdminAsync(adminId, request, admin);
        return result.ToActionResult();
    }
}
