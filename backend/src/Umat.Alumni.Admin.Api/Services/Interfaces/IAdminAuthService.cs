using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IAdminAuthService
{
    Task<IApiResponse<AdminTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<AdminTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<IApiResponse<AdminProfileResponse>> GetProfileAsync(AuthData auth);
    Task<IApiResponse<AdminTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
}
