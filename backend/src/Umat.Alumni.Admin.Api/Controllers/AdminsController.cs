using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// Authentication endpoints for admin users.
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/auth")]
public class AdminsController(IAdminAuthService authService) : DefaultController
{
    /// <summary>
    /// Authenticate an admin user and receive JWT tokens.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [SwaggerOperation(Summary = "Admin login", Description = "Authenticate with email and password to receive access and refresh tokens.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AdminTokenResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return result.ToActionResult();
    }

    /// <summary>
    /// Refresh an expired access token using a valid refresh token.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refreshtoken")]
    [SwaggerOperation(Summary = "Refresh token", Description = "Exchange a valid refresh token for new access and refresh tokens.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AdminTokenResponse>))]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await authService.RefreshTokenAsync(request);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get the currently authenticated admin's profile.
    /// </summary>
    [HttpGet("me")]
    [SwaggerOperation(Summary = "Get current admin profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminProfileResponse))]
    public IActionResult GetCurrentAdmin()
    {
        var admin = User.GetAccount();
        return Ok(new AdminProfileResponse(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Role, admin.GraduationYear, null));
    }

    /// <summary>
    /// Change the current admin's password.
    /// </summary>
    [HttpPut("changepassword")]
    [SwaggerOperation(Summary = "Change admin password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AdminTokenResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var admin = User.GetAccount();
        var result = await authService.ChangePasswordAsync(request, admin);
        return result.ToActionResult();
    }
}
