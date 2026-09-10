using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlatformAuthService
{
    Task<IApiResponse<PlatformTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<PlatformTokenResponse>> GoogleLoginAsync(GoogleLoginRequest request);
    /// <summary>Reads both tokens from the request's own httpOnly cookies — see AuthCookieExtensions.</summary>
    Task<IApiResponse<PlatformTokenResponse>> RefreshTokenAsync();
    Task<IApiResponse<PlatformTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
    Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<IApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request);
    Task LogoutAsync(AuthData auth);
}
