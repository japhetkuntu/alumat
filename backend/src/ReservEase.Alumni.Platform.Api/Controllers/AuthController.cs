using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Authentication endpoints for platform staff (SaaS operators).
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(IPlatformAuthService authService) : DefaultController
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Platform staff login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformTokenResponse>))]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpPost("refreshtoken")]
    [SwaggerOperation(Summary = "Refresh token")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformTokenResponse>))]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await authService.RefreshTokenAsync(request);
        return result.ToActionResult();
    }

    [HttpGet("me")]
    [SwaggerOperation(Summary = "Get current platform staff profile")]
    public IActionResult GetCurrentStaff()
    {
        var user = User.GetAccount();
        return Ok(new AuthUserResponse(user.Id, user.Email, user.Name, user.Role));
    }

    [HttpPut("changepassword")]
    [SwaggerOperation(Summary = "Change password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PlatformTokenResponse>))]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await authService.ChangePasswordAsync(request, User.GetAccount());
        return result.ToActionResult();
    }
}
