using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IInstitutionAuthService
{
    Task<IApiResponse<InstitutionTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<InstitutionTokenResponse>> GoogleLoginAsync(GoogleLoginRequest request);
    /// <summary>Reads both tokens from the request's own httpOnly cookies — see AuthCookieExtensions.</summary>
    Task<IApiResponse<InstitutionTokenResponse>> RefreshTokenAsync();
    Task<IApiResponse<InstitutionStaffProfileResponse>> GetProfileAsync(AuthData auth);
    Task<IApiResponse<InstitutionTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
    Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<IApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request);
    Task LogoutAsync(AuthData auth);
}
