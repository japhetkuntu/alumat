using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlatformAuthService
{
    Task<IApiResponse<PlatformTokenResponse>> LoginAsync(LoginRequest request);
    Task<IApiResponse<PlatformTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    Task<IApiResponse<PlatformTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth);
}
