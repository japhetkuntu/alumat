using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;

namespace Umat.Alumni.Member.Api.Controllers;

[Route("api/v{version:apiVersion}/auth")]
public class MembersController(IMemberAuthService authService) : DefaultController
{
    [HttpPost("register")]
    [SwaggerOperation(Summary = "Register", Description = "Submit registration and receive an OTP for email verification")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);
        return result.ToActionResult();
    }

    [HttpPost("verify-otp")]
    [SwaggerOperation(Summary = "Verify OTP", Description = "Verify the email OTP to complete registration")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var result = await authService.VerifyOtpAsync(request);
        return result.ToActionResult();
    }

    [HttpPost("resend-otp")]
    [SwaggerOperation(Summary = "Resend OTP", Description = "Resend the email verification OTP (maximum 3 resend attempts)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
    {
        var result = await authService.ResendOtpAsync(request);
        return result.ToActionResult();
    }

    [HttpPost("send-email-verification")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Send email verification link", Description = "Send a verification link to the provided email address.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SendEmailVerification([FromBody] SendEmailVerificationRequest request)
    {
        var result = await authService.SendEmailVerificationLinkAsync(request);
        return result.ToActionResult();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Forgot password", Description = "Send a password reset link to the provided email address.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await authService.ForgotPasswordAsync(request);
        return result.ToActionResult();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Reset password", Description = "Reset a member's password using a reset token.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await authService.ResetPasswordAsync(request);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    [SwaggerOperation(Summary = "Login", Description = "Authenticate a member and return tokens")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberTokenResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return result.ToActionResult();
    }

    [HttpPost("refreshtoken")]
    [SwaggerOperation(Summary = "Refresh token", Description = "Exchange a refresh token for new access/refresh tokens")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberTokenResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await authService.RefreshTokenAsync(request);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("me")]
    [SwaggerOperation(Summary = "Get profile", Description = "Get the current member's profile details")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberProfileResponse>))]
    public async Task<IActionResult> GetProfile()
    {
        var member = User.GetAccount();
        var result = await authService.GetProfileAsync(member);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPut("me")]
    [SwaggerOperation(Summary = "Update profile", Description = "Update the current member's profile information")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberProfileResponse>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
    {
        var member = User.GetAccount();
        var result = await authService.UpdateProfileAsync(request, member);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPut("changepassword")]
    [SwaggerOperation(Summary = "Change password", Description = "Change the current member's password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var member = User.GetAccount();
        var result = await authService.ChangePasswordAsync(request, member);
        return result.ToActionResult();
    }

    [HttpGet("verify-email")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Verify email", Description = "Verify member email address with the token from the verification link")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token, [FromQuery] string email)
    {
        var result = await authService.VerifyEmailAsync(token, email);
        return result.ToActionResult();
    }
}
