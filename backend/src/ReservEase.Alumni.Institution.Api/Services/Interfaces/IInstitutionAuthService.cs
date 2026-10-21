using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IInstitutionAuthService
{
    Task<IApiResponse<InstitutionTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<InstitutionTokenResponse>> GoogleLoginAsync(GoogleLoginRequest request);
    Task<IApiResponse<InstitutionTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<IApiResponse<InstitutionStaffProfileResponse>> GetProfileAsync(AuthData auth);
    Task<IApiResponse<InstitutionTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
    Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<IApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>Emails a new staff member a link to choose their own password (the same link "Forgot password" sends). Nobody ever types a password for someone else.</summary>
    Task SendPasswordSetupLinkAsync(ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff staff);
}
