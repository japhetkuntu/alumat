using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IInstitutionAuthService
{
    Task<IApiResponse<InstitutionTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<InstitutionTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<IApiResponse<InstitutionStaffProfileResponse>> GetProfileAsync(AuthData auth);
    Task<IApiResponse<InstitutionTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
    Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<IApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request);
}
