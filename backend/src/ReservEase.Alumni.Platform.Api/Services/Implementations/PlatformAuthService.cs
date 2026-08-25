using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Options;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class PlatformAuthService(
    IAlumniPgRepository<PlatformStaff> staffRepo,
    IRedisService<PlatformRedisConfig> redis,
    IOptions<BearerTokenConfig> tokenConfigOptions,
    IOptions<MailtrapConfig> mailtrapConfigOptions,
    IEmailService emailService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<PlatformAuthService> logger) : IPlatformAuthService
{
    private readonly BearerTokenConfig tokenConfig = tokenConfigOptions.Value;
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;

    /// <summary>The current request's own scheme+host — reset links must point back to this Platform Portal host, not a fixed domain.</summary>
    private string GetRequestBaseUrl()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        return request is null ? "https://example.com" : $"{request.Scheme}://{request.Host}";
    }

    private static string GenerateUrlToken(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private async Task SendResetPasswordEmailAsync(string name, string email, string token, string baseUrl)
    {
        try
        {
            var link = $"{baseUrl}/reset-password?token={token}&email={Uri.EscapeDataString(email)}";
            await emailService.SendEmailAsync(new SendEmailRequest
            {
                To = [new EmailContact { Email = email, Name = name }],
                TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.ResetPassword)
                    ? "reset-password"
                    : mailtrapConfig.Templates.ResetPassword,
                TemplateVariables = new { first_name = name, reset_url = link },
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send reset password email to {Email}", email);
        }
    }

    public async Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        try
        {
            var email = request.Email.ToLower().Trim();
            var staff = await staffRepo.GetOneAsync(s => s.Email == email);
            if (staff is null)
                return new object().ToOkApiResponse("Password reset instructions sent if the account exists.");

            var token = GenerateUrlToken("reset");
            staff.PasswordResetToken = token;
            staff.PasswordResetSentAt = DateTime.UtcNow;
            await staffRepo.UpdateAsync(staff);

            _ = SendResetPasswordEmailAsync(staff.Name, staff.Email, token, GetRequestBaseUrl());
            return new object().ToOkApiResponse("Password reset instructions sent to your email.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending forgot password email to {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to send password reset email");
        }
    }

    public async Task<IApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var email = request.Email.ToLower().Trim();
            var staff = await staffRepo.GetOneAsync(s => s.Email == email);
            if (staff is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid reset token or email.");

            if (string.IsNullOrWhiteSpace(staff.PasswordResetToken) || staff.PasswordResetToken != request.Token)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid reset token or email.");

            if (staff.PasswordResetSentAt is null || staff.PasswordResetSentAt < DateTime.UtcNow.AddHours(-24))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Password reset token has expired. Please request a new link.");

            staff.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            staff.PasswordResetToken = null;
            staff.PasswordResetSentAt = null;
            staff.UpdatedAt = DateTime.UtcNow;
            await staffRepo.UpdateAsync(staff);

            return new object().ToOkApiResponse("Password has been reset successfully.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error resetting password for {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reset password");
        }
    }

    public async Task<IApiResponse<PlatformTokenResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var staff = await staffRepo.GetOneAsync(s => s.Email == request.Email.ToLower().Trim());
            if (staff is null || !BCrypt.Net.BCrypt.Verify(request.Password, staff.Password))
                return ApiResponseExtensions.ToBadRequestApiResponse<PlatformTokenResponse>("Invalid email or password");

            if (staff.IsDisabled)
                return ApiResponseExtensions.ToUnauthorizedApiResponse<PlatformTokenResponse>("Account disabled");

            staff.LastActiveAt = DateTime.UtcNow;
            await staffRepo.UpdateAsync(staff);

            var claimData = BuildClaimData(staff);
            var accessToken = GenerateJwtToken(claimData);
            var refreshToken = GenerateRefreshToken();

            await redis.SetAsync($"platform:refresh:{staff.Id}", refreshToken,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            logger.LogInformation("Platform staff {StaffId} logged in successfully", staff.Id);

            var user = new AuthUserResponse(staff.Id, staff.Email, staff.Name, staff.Role);
            var tokensResp = new AuthTokensResponse(accessToken, refreshToken, tokenConfig.AccessTokenLifetime * 3600);
            return new PlatformTokenResponse(user, tokensResp).ToOkApiResponse("Login successful");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during platform login for email: {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformTokenResponse>("Login failed");
        }
    }

    public async Task<IApiResponse<PlatformTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        try
        {
            var staffId = ExtractUserIdFromExpiredToken(request.AccessToken);
            if (string.IsNullOrEmpty(staffId))
                return ApiResponseExtensions.ToUnauthorizedApiResponse<PlatformTokenResponse>("Invalid token");

            var stored = await redis.GetAsync<string>($"platform:refresh:{staffId}");
            if (stored is null || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(request.RefreshToken)))
                return ApiResponseExtensions.ToUnauthorizedApiResponse<PlatformTokenResponse>("Invalid or expired refresh token");

            var staff = await staffRepo.GetByIdAsync(staffId);
            if (staff is null)
                return ApiResponseExtensions.ToUnauthorizedApiResponse<PlatformTokenResponse>("Invalid or expired refresh token");

            var claimData = BuildClaimData(staff);
            var accessToken = GenerateJwtToken(claimData);
            var newRefresh = GenerateRefreshToken();
            await redis.SetAsync($"platform:refresh:{staff.Id}", newRefresh,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            var user = new AuthUserResponse(staff.Id, staff.Email, staff.Name, staff.Role);
            var tokensResp = new AuthTokensResponse(accessToken, newRefresh, tokenConfig.AccessTokenLifetime * 3600);
            return new PlatformTokenResponse(user, tokensResp).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during platform token refresh");
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformTokenResponse>("Token refresh failed");
        }
    }

    public async Task<IApiResponse<PlatformTokenResponse>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth)
    {
        try
        {
            var staff = await staffRepo.GetByIdAsync(auth.Id);
            if (staff is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PlatformTokenResponse>("Staff account not found");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, staff.Password))
                return ApiResponseExtensions.ToBadRequestApiResponse<PlatformTokenResponse>("Current password is incorrect");

            staff.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            staff.UpdatedAt = DateTime.UtcNow;
            staff.UpdatedBy = auth.Id;
            await staffRepo.UpdateAsync(staff);

            await redis.RemoveAsync($"platform:refresh:{staff.Id}");

            var claimData = BuildClaimData(staff);
            var accessToken = GenerateJwtToken(claimData);
            var refreshToken = GenerateRefreshToken();
            await redis.SetAsync($"platform:refresh:{staff.Id}", refreshToken,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            var user = new AuthUserResponse(staff.Id, staff.Email, staff.Name, staff.Role);
            var tokensResp = new AuthTokensResponse(accessToken, refreshToken, tokenConfig.AccessTokenLifetime * 3600);
            return new PlatformTokenResponse(user, tokensResp).ToOkApiResponse("Password changed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error changing password for platform staff {StaffId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformTokenResponse>("Failed to change password");
        }
    }

    private string GenerateJwtToken(PlatformAuthClaimData auth)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, auth.Id),
            new Claim(ClaimTypes.NameIdentifier, auth.Id),
            new Claim(ClaimTypes.Email, auth.Email),
            new Claim(ClaimTypes.GivenName, auth.Name),
            new Claim(ClaimTypes.Role, auth.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: auth.Issuer,
            audience: auth.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(auth.DurationInHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private PlatformAuthClaimData BuildClaimData(PlatformStaff staff) => new()
    {
        Id = staff.Id,
        Email = staff.Email,
        Name = staff.Name,
        Role = staff.Role,
        SigningKey = tokenConfig.PlatformSigningKey,
        Issuer = tokenConfig.Issuer,
        Audience = tokenConfig.Audience,
        DurationInHours = tokenConfig.AccessTokenLifetime,
    };

    private string? ExtractUserIdFromExpiredToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenConfig.PlatformSigningKey));
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = tokenConfig.Issuer,
                ValidateAudience = true,
                ValidAudience = tokenConfig.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = false,
            }, out _);

            return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
        }
    }
}
