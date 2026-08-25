using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Authentication endpoints for institution staff.
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(IInstitutionAuthService authService) : DefaultController
{
    /// <summary>
    /// Authenticate an institution staff member and receive JWT tokens.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Institution staff login", Description = "Authenticate with email and password to receive access and refresh tokens.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionTokenResponse>))]
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionTokenResponse>))]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await authService.RefreshTokenAsync(request);
        return result.ToActionResult();
    }

    /// <summary>
    /// Send a password reset link to the provided email address, if an eligible staff account exists.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Forgot password", Description = "Send a password reset link to the provided staff email address.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await authService.ForgotPasswordAsync(request);
        return result.ToActionResult();
    }

    /// <summary>
    /// Reset a staff member's password using a reset token from the emailed link.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Reset password", Description = "Reset a staff member's password using a reset token.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await authService.ResetPasswordAsync(request);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get the currently authenticated staff member's profile.
    /// </summary>
    [HttpGet("me")]
    [SwaggerOperation(Summary = "Get current staff profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InstitutionStaffProfileResponse))]
    public IActionResult GetCurrentStaff()
    {
        var admin = User.GetAccount();
        return Ok(new InstitutionStaffProfileResponse(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Role, admin.YearGroups, admin.CommunityIds, null));
    }

    /// <summary>
    /// Change the current staff member's password.
    /// </summary>
    [HttpPut("changepassword")]
    [SwaggerOperation(Summary = "Change staff password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionTokenResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var admin = User.GetAccount();
        var result = await authService.ChangePasswordAsync(request, admin);
        return result.ToActionResult();
    }
}
