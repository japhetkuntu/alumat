using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IMemberAuthService
{
    Task<IApiResponse<object>> RegisterAsync(RegisterRequest request);
    Task<IApiResponse<object>> VerifyOtpAsync(VerifyOtpRequest request);
    Task<IApiResponse<object>> ResendOtpAsync(ResendOtpRequest request);
    Task<IApiResponse<object>> VerifyEmailAsync(string token, string email);
    Task<IApiResponse<object>> SendEmailVerificationLinkAsync(SendEmailVerificationRequest request);
    Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<IApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request);
    Task<IApiResponse<MemberTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<MemberTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<IApiResponse<MemberProfileResponse>> GetProfileAsync(AuthData auth);
    Task<IApiResponse<MemberProfileResponse>> UpdateProfileAsync(UpdateProfileRequest request, AuthData auth);
    Task<IApiResponse<object>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
}
