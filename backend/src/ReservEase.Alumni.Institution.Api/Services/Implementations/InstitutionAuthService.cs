using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class InstitutionAuthService(
    IAlumniPgRepository<StaffEntity> adminRepo,
    IRedisService<InstitutionRedisConfig> redis,
    IOptions<BearerTokenConfig> tokenConfigOptions,
    ILogger<InstitutionAuthService> logger) : IInstitutionAuthService
{
    private const string PictureClaimType = "picture";
    private readonly BearerTokenConfig tokenConfig = tokenConfigOptions.Value;

    public async Task<IApiResponse<InstitutionTokenResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            logger.LogInformation("Login attempt for email: {Email}", request.Email);

            var admin = await adminRepo.GetOneAsync(a => a.Email == request.Email.ToLower().Trim());
            if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.Password))
                return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionTokenResponse>("Invalid email or password");

            if (admin.IsDisabled)
            {
                logger.LogWarning("Login attempt for disabled admin {AdminId} (email: {Email})", admin.Id, request.Email);
                return ApiResponseExtensions.ToUnauthorizedApiResponse<InstitutionTokenResponse>("Account disabled");
            }

            admin.LastLoginAt = DateTime.UtcNow;
            await adminRepo.UpdateAsync(admin);

            var claimData = BuildClaimData(admin);
            var accessToken = GenerateJwtToken(claimData);
            var refreshToken = GenerateRefreshToken();

            await redis.SetAsync($"admin:refresh:{admin.Id}", refreshToken,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            logger.LogInformation("Admin {AdminId} logged in successfully", admin.Id);

            var user = new AuthUserResponse(admin.Id, admin.Email, admin.FirstName, admin.LastName, admin.Role, admin.YearGroup, null);
            var tokensResp = new AuthTokensResponse(accessToken, refreshToken, tokenConfig.AccessTokenLifetime * 3600);
            return new InstitutionTokenResponse(user, tokensResp).ToOkApiResponse("Login successful");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during login for email: {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionTokenResponse>("Login failed");
        }
    }

    public async Task<IApiResponse<InstitutionTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        try
        {
            logger.LogInformation("Refresh token request received");

            var adminId = ExtractUserIdFromExpiredToken(request.AccessToken, tokenConfig.InstitutionSigningKey);
            if (string.IsNullOrEmpty(adminId))
                return ApiResponseExtensions.ToUnauthorizedApiResponse<InstitutionTokenResponse>("Invalid token");

            var stored = await redis.GetAsync<string>($"admin:refresh:{adminId}");
            if (stored is null || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(request.RefreshToken)))
                return ApiResponseExtensions.ToUnauthorizedApiResponse<InstitutionTokenResponse>("Invalid or expired refresh token");

            var admin = await adminRepo.GetByIdAsync(adminId);
            if (admin is null)
                return ApiResponseExtensions.ToUnauthorizedApiResponse<InstitutionTokenResponse>("Invalid or expired refresh token");

            var claimData = BuildClaimData(admin);
            var accessToken = GenerateJwtToken(claimData);
            var newRefresh = GenerateRefreshToken();
            await redis.SetAsync($"admin:refresh:{admin.Id}", newRefresh,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            logger.LogInformation("Tokens refreshed for admin {AdminId}", admin.Id);

            var user = new AuthUserResponse(admin.Id, admin.Email, admin.FirstName, admin.LastName, admin.Role, admin.YearGroup, null);
            var tokensResp = new AuthTokensResponse(accessToken, newRefresh, tokenConfig.AccessTokenLifetime * 3600);
            return new InstitutionTokenResponse(user, tokensResp).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during token refresh");
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionTokenResponse>("Token refresh failed");
        }
    }

    public async Task<IApiResponse<InstitutionStaffProfileResponse>> GetProfileAsync(AuthData auth)
    {
        try
        {
            logger.LogInformation("GetProfile request by admin {AdminId}", auth.Id);

            var admin = await adminRepo.GetByIdAsync(auth.Id);
            if (admin is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionStaffProfileResponse>("Admin not found");

            return new InstitutionStaffProfileResponse(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.Role, admin.YearGroup, null)
                .ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving profile for admin {AdminId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionStaffProfileResponse>("Failed to retrieve profile");
        }
    }

    public async Task<IApiResponse<InstitutionTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth)
    {
        try
        {
            logger.LogInformation("ChangePassword request by admin {AdminId}", auth.Id);

            var admin = await adminRepo.GetByIdAsync(auth.Id);
            if (admin is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionTokenResponse>("Admin not found");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, admin.Password))
                return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionTokenResponse>("Current password is incorrect");

            admin.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            admin.UpdatedAt = DateTime.UtcNow;
            admin.UpdatedBy = auth.Id;
            await adminRepo.UpdateAsync(admin);

            // Invalidate old refresh token before issuing new one
            await redis.RemoveAsync($"admin:refresh:{admin.Id}");

            var claimData = BuildClaimData(admin);
            var accessToken = GenerateJwtToken(claimData);
            var refreshToken = GenerateRefreshToken();
            await redis.SetAsync($"admin:refresh:{admin.Id}", refreshToken,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            logger.LogInformation("Password changed successfully for admin {AdminId}", admin.Id);

            var user = new AuthUserResponse(admin.Id, admin.Email, admin.FirstName, admin.LastName, admin.Role, admin.YearGroup, null);
            var tokensResp = new AuthTokensResponse(accessToken, refreshToken, tokenConfig.AccessTokenLifetime * 3600);
            return new InstitutionTokenResponse(user, tokensResp).ToOkApiResponse("Password changed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error changing password for admin {AdminId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionTokenResponse>("Failed to change password");
        }
    }

    public string GenerateJwtToken(AuthClaimData auth)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, auth.Id),
            new Claim(ClaimTypes.NameIdentifier, auth.Id),
            new Claim(ClaimTypes.Email, auth.Email),
            new Claim(ClaimTypes.GivenName, auth.FirstName),
            new Claim(ClaimTypes.Surname, auth.LastName),
            new Claim(ClaimTypes.Role, auth.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (auth.GraduationYear.HasValue)
        {
            claims.Add(new Claim("year_group", auth.GraduationYear.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(auth.InstitutionId))
        {
            // Informational only — which tenant this account belongs to.
            // Not the security boundary: AlumniDbContext's query filter, sourced
            // from the resolved Host (TenantResolutionMiddleware), is.
            claims.Add(new Claim("institution_id", auth.InstitutionId));
        }

        if (!string.IsNullOrWhiteSpace(auth.ProfilePictureUrl))
        {
            claims.Add(new Claim(PictureClaimType, auth.ProfilePictureUrl));
        }

        var token = new JwtSecurityToken(
            issuer: auth.Issuer,
            audience: auth.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(auth.DurationInHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private string? ExtractUserIdFromExpiredToken(string token, string signingKey)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = tokenConfig.Issuer,
                ValidateAudience = true,
                ValidAudience = tokenConfig.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = false, // allow expired tokens
            }, out _);
            return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
        }
    }

    private AuthClaimData BuildClaimData(StaffEntity admin) => new()
    {
        Id = admin.Id,
        Email = admin.Email,
        FirstName = admin.FirstName,
        LastName = admin.LastName,
        ProfilePictureUrl = null,
        Role = admin.Role,
        GraduationYear = admin.YearGroup,
        InstitutionId = admin.InstitutionId,
        SigningKey = tokenConfig.InstitutionSigningKey,
        Issuer = tokenConfig.Issuer,
        Audience = tokenConfig.Audience,
        DurationInHours = tokenConfig.AccessTokenLifetime,
    };
}
