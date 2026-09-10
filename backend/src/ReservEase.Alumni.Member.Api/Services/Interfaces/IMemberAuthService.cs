using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberAuthService
{
    Task<IApiResponse<object>> RegisterAsync(RegisterRequest request);
    Task<IApiResponse<object>> GoogleRegisterAsync(GoogleRegisterRequest request);
    Task<IApiResponse<object>> VerifyOtpAsync(VerifyOtpRequest request);
    Task<IApiResponse<object>> ResendOtpAsync(ResendOtpRequest request);
    Task<IApiResponse<object>> VerifyEmailAsync(string token, string email);
    Task<IApiResponse<object>> SendEmailVerificationLinkAsync(SendEmailVerificationRequest request);
    Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<IApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request);
    Task<IApiResponse<MemberTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<MemberTokenResponse>> GoogleLoginAsync(GoogleLoginRequest request);
    /// <summary>Reads both tokens from the request's own httpOnly cookies — see AuthCookieExtensions.</summary>
    Task<IApiResponse<MemberTokenResponse>> RefreshTokenAsync();
    Task<IApiResponse<MemberProfileResponse>> GetProfileAsync(AuthData auth);
    Task<IApiResponse<MemberProfileResponse>> UpdateProfileAsync(UpdateProfileRequest request, AuthData auth);
    Task<IApiResponse<object>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
    Task LogoutAsync(AuthData auth);
}
