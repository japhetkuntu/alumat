using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Common.Sdk.Options;
using Umat.Alumni.Mailtrap.Sdk.Models;
using Umat.Alumni.Mailtrap.Sdk.Options;
using Umat.Alumni.Mailtrap.Sdk.Services;
using Umat.Alumni.Member.Api.Extensions;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Options;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Redis.Sdk.Services;
using Umat.Alumni.Storage.Sdk.Services;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Referral = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Referral;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class MemberAuthService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Referral> referralRepo,
    IRedisService<MemberRedisConfig> redis,
    IOptions<BearerTokenConfig> tokenConfigOptions,
    IOptions<MailtrapConfig> mailtrapConfigOptions,
    IEmailService emailService,
    IStorageService storageService,
    ILogger<MemberAuthService> logger) : IMemberAuthService
{
    private const string PictureClaimType = "picture";
    private readonly BearerTokenConfig tokenConfig = tokenConfigOptions.Value;
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;

    public async Task<IApiResponse<object>> RegisterAsync(RegisterRequest request)
    {
        try
        {
            logger.LogInformation("Register request for email: {Email}", request.Email);

            var email = request.Email.ToLower().Trim();
            var existing = await memberRepo.GetOneAsync(m => m.Email == email);

            // Allow re-registration for suspended (rejected) members
            if (existing is not null && existing.Status != "Suspended")
                return ApiResponseExtensions.ToConflictApiResponse<object>("Email already registered");

            // If suspended, remove old record so they can re-register
            if (existing is not null && existing.Status == "Suspended")
                await memberRepo.RemoveAsync(existing);

            // Generate 6-digit OTP
            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            // Cache registration data in Redis (15 min TTL)
            var cached = new CachedRegistration
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Phone = request.Phone,
                StudentId = request.StudentId,
                GraduationYear = request.GraduationYear,
                DepartmentId = request.DepartmentId,
                Otp = otp,
                ResendCount = 0,
                ReferralCode = request.ReferralCode?.Trim(),
            };

            await redis.SetAsync($"reg:otp:{email}", cached, TimeSpan.FromMinutes(15));

            // Send OTP email (fire-and-forget)
            _ = SendOtpEmailAsync(cached.FirstName, email, otp);

            logger.LogInformation("OTP sent for registration, email: {Email}", email);
            return new object().ToOkApiResponse("Verification code sent to your email. Please check your inbox.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during registration for email: {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Registration failed");
        }
    }

    public async Task<IApiResponse<object>> VerifyOtpAsync(VerifyOtpRequest request)
    {
        try
        {
            logger.LogInformation("VerifyOtp request for email: {Email}", request.Email);

            var email = request.Email.ToLower().Trim();
            var cached = await redis.GetAsync<CachedRegistration>($"reg:otp:{email}");
            if (cached is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("No pending registration found. Please register again.");

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(cached.Otp), Encoding.UTF8.GetBytes(request.Otp)))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid verification code");

            // OTP valid — save member to DB for admin approval (member number assigned by admin later)
            var member = new MemberEntity
            {
                FirstName = cached.FirstName,
                LastName = cached.LastName,
                Email = cached.Email,
                Password = cached.PasswordHash,
                Phone = cached.Phone,
                StudentId = cached.StudentId,
                GraduationYear = cached.GraduationYear,
                DepartmentId = cached.DepartmentId ?? string.Empty,
                Status = "Pending",
                IsEmailVerified = true,
                CreatedBy = "self",
            };

            // Link referral if provided
            if (!string.IsNullOrWhiteSpace(cached.ReferralCode))
            {
                var referrer = await memberRepo.GetOneAsync(m => m.ReferralCode == cached.ReferralCode);
                if (referrer is not null)
                {
                    member.ReferredById = referrer.Id;
                }
            }

            await memberRepo.AddAsync(member);

            // Update referral record status
            if (!string.IsNullOrWhiteSpace(member.ReferredById))
            {
                var referral = await referralRepo.GetOneAsync(r =>
                    r.ReferrerId == member.ReferredById && r.ReferredEmail == cached.Email);
                if (referral is not null)
                {
                    referral.ReferredMemberId = member.Id;
                    referral.Status = "Registered";
                    await referralRepo.UpdateAsync(referral);
                }
            }

            // Clean up Redis
            await redis.RemoveAsync($"reg:otp:{email}");

            logger.LogInformation("Member {MemberId} registered successfully after OTP verification", member.Id);
            return new object().ToCreatedApiResponse("Email verified successfully. Your account is pending admin approval.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during OTP verification for email: {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("OTP verification failed");
        }
    }

    public async Task<IApiResponse<object>> ResendOtpAsync(ResendOtpRequest request)
    {
        try
        {
            logger.LogInformation("ResendOtp request for email: {Email}", request.Email);

            var email = request.Email.ToLower().Trim();
            var cached = await redis.GetAsync<CachedRegistration>($"reg:otp:{email}");
            if (cached is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("No pending registration found. Please register again.");

            if (cached.ResendCount >= 3)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Maximum resend attempts reached. Please register again.");

            // Generate new OTP
            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            cached.Otp = otp;
            cached.ResendCount++;

            await redis.SetAsync($"reg:otp:{email}", cached, TimeSpan.FromMinutes(15));

            // Send OTP email (fire-and-forget)
            _ = SendOtpEmailAsync(cached.FirstName, email, otp);

            var remaining = 3 - cached.ResendCount;
            logger.LogInformation("OTP resent for email: {Email}, remaining attempts: {Remaining}", email, remaining);
            return new object().ToOkApiResponse($"Verification code resent. {remaining} resend attempt(s) remaining.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error resending OTP for email: {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to resend verification code");
        }
    }

    public async Task<IApiResponse<object>> SendEmailVerificationLinkAsync(SendEmailVerificationRequest request)
    {
        try
        {
            var email = request.Email.ToLower().Trim();
            var member = await memberRepo.GetOneAsync(m => m.Email == email);
            if (member is null)
                return ApiResponseExtensions.ToOkApiResponse<object>("Verification email sent if the account exists.");

            if (member.IsEmailVerified)
                return new object().ToOkApiResponse("Email is already verified.");

            var token = GenerateUrlToken("verify");
            member.EmailVerificationToken = token;
            member.EmailVerificationSentAt = DateTime.UtcNow;
            await memberRepo.UpdateAsync(member);

            _ = SendVerificationLinkEmailAsync(member.FirstName, member.Email, token);
            return new object().ToOkApiResponse("Verification link sent to your email.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending email verification link to {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to send verification email");
        }
    }

    public async Task<IApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        try
        {
            var email = request.Email.ToLower().Trim();
            var member = await memberRepo.GetOneAsync(m => m.Email == email);
            if (member is null)
                return new object().ToOkApiResponse("Password reset instructions sent if the account exists.");

            var token = GenerateUrlToken("reset");
            member.EmailVerificationToken = token;
            member.EmailVerificationSentAt = DateTime.UtcNow;
            await memberRepo.UpdateAsync(member);

            _ = SendResetPasswordEmailAsync(member.FirstName, member.Email, token);
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
            var member = await memberRepo.GetOneAsync(m => m.Email == email);
            if (member is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid reset token or email.");

            if (string.IsNullOrWhiteSpace(member.EmailVerificationToken) || !member.EmailVerificationToken.StartsWith("reset_"))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid reset token or email.");

            if (member.EmailVerificationToken != request.Token)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid reset token or email.");

            if (member.EmailVerificationSentAt is null || member.EmailVerificationSentAt < DateTime.UtcNow.AddHours(-24))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Password reset token has expired. Please request a new link.");

            member.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            member.EmailVerificationToken = null;
            member.EmailVerificationSentAt = null;
            member.UpdatedAt = DateTime.UtcNow;
            await memberRepo.UpdateAsync(member);

            return new object().ToOkApiResponse("Password has been reset successfully.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error resetting password for {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reset password");
        }
    }

    private static string GenerateUrlToken(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }

    private async Task SendVerificationLinkEmailAsync(string firstName, string email, string token)
    {
        try
        {
            var link = $"https://alumni.umat.edu.gh/auth/verify-email?token={token}&email={Uri.EscapeDataString(email)}";
            await emailService.SendEmailAsync(new SendEmailRequest
            {
                To = [new EmailContact { Email = email, Name = firstName }],
                TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.EmailVerificationLink)
                    ? "email-verification-link"
                    : mailtrapConfig.Templates.EmailVerificationLink,
                TemplateVariables = new { first_name = firstName, verify_url = link },
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send verification link email to {Email}", email);
        }
    }

    private async Task SendResetPasswordEmailAsync(string firstName, string email, string token)
    {
        try
        {
            var link = $"https://alumni.umat.edu.gh/reset-password?token={token}&email={Uri.EscapeDataString(email)}";
            await emailService.SendEmailAsync(new SendEmailRequest
            {
                To = [new EmailContact { Email = email, Name = firstName }],
                TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.ResetPassword)
                    ? "reset-password"
                    : mailtrapConfig.Templates.ResetPassword,
                TemplateVariables = new { first_name = firstName, reset_url = link },
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send reset password email to {Email}", email);
        }
    }

    public async Task<IApiResponse<MemberTokenResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            logger.LogInformation("Login attempt for email: {Email}", request.Email);

            var member = await memberRepo.GetOneAsync(m => m.Email == request.Email.ToLower().Trim());
            if (member is null || !BCrypt.Net.BCrypt.Verify(request.Password, member.Password))
                return ApiResponseExtensions.ToBadRequestApiResponse<MemberTokenResponse>("Invalid email or password");

            if (member.Status is "Pending")
                return ApiResponseExtensions.ToBadRequestApiResponse<MemberTokenResponse>("Your account is pending admin approval.");
            if (member.Status is "Banned")
                return ApiResponseExtensions.ToBadRequestApiResponse<MemberTokenResponse>("Your account has been banned. Please contact support.");
            if (member.Status is "Blocked")
                return ApiResponseExtensions.ToBadRequestApiResponse<MemberTokenResponse>("Your account has been permanently blocked.");
            if (member.Status is "Suspended")
                return ApiResponseExtensions.ToBadRequestApiResponse<MemberTokenResponse>("Your registration was rejected. You may submit a new registration request.");

            member.LastLoginAt = DateTime.UtcNow;
            await memberRepo.UpdateAsync(member);

            var claimData = BuildClaimData(member);
            var accessToken = GenerateJwtToken(claimData);
            var refreshToken = GenerateRefreshToken();
            await redis.SetAsync($"member:refresh:{member.Id}", refreshToken,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            var userResp = new AuthUserResponse(member.Id, member.Email, member.FirstName, member.LastName, "Member", member.GraduationYear, member.ProfilePictureUrl);
            var tokensResp = new AuthTokensResponse(accessToken, refreshToken, tokenConfig.AccessTokenLifetime * 3600);

            logger.LogInformation("Member {MemberId} logged in successfully", member.Id);
            return new MemberTokenResponse(userResp, tokensResp).ToOkApiResponse("Login successful");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during login for email: {Email}", request.Email);
            return ApiResponseExtensions.ToServerErrorApiResponse<MemberTokenResponse>("Login failed");
        }
    }

    public async Task<IApiResponse<MemberTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        try
        {
            logger.LogInformation("RefreshToken request received");

            // Extract member ID from the expired access token
            var memberId = ExtractUserIdFromExpiredToken(request.AccessToken, tokenConfig.MemberSigningKey);
            if (string.IsNullOrEmpty(memberId))
                return ApiResponseExtensions.ToUnauthorizedApiResponse<MemberTokenResponse>("Invalid token");

            var stored = await redis.GetAsync<string>($"member:refresh:{memberId}");
            if (stored is null || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(request.RefreshToken)))
                return ApiResponseExtensions.ToUnauthorizedApiResponse<MemberTokenResponse>("Invalid or expired refresh token");

            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null)
                return ApiResponseExtensions.ToUnauthorizedApiResponse<MemberTokenResponse>("Invalid or expired refresh token");

            var claimData = BuildClaimData(member);
            var accessToken = GenerateJwtToken(claimData);
            var newRefresh = GenerateRefreshToken();
            await redis.SetAsync($"member:refresh:{member.Id}", newRefresh,
                TimeSpan.FromDays(tokenConfig.RefreshTokenLifetime));

            var userResp = new AuthUserResponse(member.Id, member.Email, member.FirstName, member.LastName, "Member", member.GraduationYear, member.ProfilePictureUrl);
            var tokensResp = new AuthTokensResponse(accessToken, newRefresh, tokenConfig.AccessTokenLifetime * 3600);
            return new MemberTokenResponse(userResp, tokensResp).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during token refresh");
            return ApiResponseExtensions.ToServerErrorApiResponse<MemberTokenResponse>("Token refresh failed");
        }
    }

    public async Task<IApiResponse<MemberProfileResponse>> GetProfileAsync(AuthData auth)
    {
        try
        {
            logger.LogInformation("GetProfile request for member {MemberId}", auth.Id);

            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<MemberProfileResponse>("Member not found");

            return ToProfile(member).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving profile for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<MemberProfileResponse>("Failed to retrieve profile");
        }
    }

    public async Task<IApiResponse<MemberProfileResponse>> UpdateProfileAsync(UpdateProfileRequest request, AuthData auth)
    {
        try
        {
            logger.LogInformation("UpdateProfile request: {Request} for member {MemberId}", request.Serialize(), auth.Id);

            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<MemberProfileResponse>("Member not found");

            member.Company = request.Company ?? member.Company;
            member.JobTitle = request.JobTitle ?? member.JobTitle;
            member.Location = request.Location ?? member.Location;
            member.LinkedInUrl = request.LinkedInUrl ?? member.LinkedInUrl;
            member.Bio = request.Bio ?? member.Bio;
            member.Phone = request.Phone ?? member.Phone;

            if (request.EmploymentStatus is "Employed" or "Pensioner")
            {
                if (member.EmploymentStatus == "Pensioner" && request.EmploymentStatus == "Employed")
                    return ApiResponseExtensions.ToBadRequestApiResponse<MemberProfileResponse>("Employment status cannot be changed back from Pensioner.");
                member.EmploymentStatus = request.EmploymentStatus;
            }

            if (request.ProfilePicture is not null)
            {
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var ext = Path.GetExtension(request.ProfilePicture.FileName);
                if (!allowedExtensions.Contains(ext))
                    return ApiResponseExtensions.ToBadRequestApiResponse<MemberProfileResponse>("Only image files (jpg, png, webp, gif) are allowed.");
                if (request.ProfilePicture.Length > 5 * 1024 * 1024)
                    return ApiResponseExtensions.ToBadRequestApiResponse<MemberProfileResponse>("Profile picture must be under 5 MB.");

                var name = $"{Guid.NewGuid():N}{ext}";
                member.ProfilePictureUrl = await storageService.UploadFileAsync(request.ProfilePicture, name);
            }

            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = auth.Id;
            await memberRepo.UpdateAsync(member);

            logger.LogInformation("Profile updated for member {MemberId}", auth.Id);
            return ToProfile(member).ToOkApiResponse("Profile updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating profile for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<MemberProfileResponse>("Failed to update profile");
        }
    }

    public async Task<IApiResponse<object>> VerifyEmailAsync(string token, string email)
    {
        try
        {
            logger.LogInformation("VerifyEmail request for email: {Email}", email);

            var member = await memberRepo.GetOneAsync(m => m.Email == email.ToLower().Trim());
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            if (member.IsEmailVerified)
                return new object().ToOkApiResponse("Email already verified");

            if (string.IsNullOrWhiteSpace(member.EmailVerificationToken) || !member.EmailVerificationToken.StartsWith("verify_"))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid or expired verification link");

            if (member.EmailVerificationToken != token)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid or expired verification link");

            if (member.EmailVerificationSentAt < DateTime.UtcNow.AddHours(-24))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Verification link has expired. Please request a new one.");

            member.IsEmailVerified = true;
            member.EmailVerificationToken = null;
            member.EmailVerificationSentAt = null;
            member.UpdatedAt = DateTime.UtcNow;
            await memberRepo.UpdateAsync(member);

            logger.LogInformation("Email verified for member {MemberId}", member.Id);
            return new object().ToOkApiResponse("Email verified successfully");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error verifying email for: {Email}", email);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Email verification failed");
        }
    }

    public async Task<IApiResponse<object>> ChangePasswordAsync(ChangePasswordRequest request, AuthData auth)
    {
        try
        {
            logger.LogInformation("ChangePassword request for member {MemberId}", auth.Id);

            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, member.Password))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Current password is incorrect");

            member.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = auth.Id;
            await memberRepo.UpdateAsync(member);

            // Invalidate existing refresh token so old sessions can't continue
            await redis.RemoveAsync($"member:refresh:{member.Id}");

            logger.LogInformation("Password changed for member {MemberId}", auth.Id);
            return new object().ToOkApiResponse("Password changed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error changing password for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to change password");
        }
    }

    private string GenerateJwtToken(MemberAuthClaimData auth)
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

    private string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private async Task SendOtpEmailAsync(string firstName, string email, string otp)
    {
        try
        {
            await emailService.SendEmailAsync(new SendEmailRequest
            {
                To = [new EmailContact { Email = email, Name = firstName }],
                TemplateId = mailtrapConfig.Templates.EmailVerification,
                TemplateVariables = new { first_name = firstName, otp_code = otp },
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send OTP email to {Email}", email);
        }
    }

    private MemberAuthClaimData BuildClaimData(MemberEntity member) => new()
    {
        Id = member.Id,
        Email = member.Email,
        FirstName = member.FirstName,
        LastName = member.LastName,
        ProfilePictureUrl = member.ProfilePictureUrl,
        Role = "Member",
        SigningKey = tokenConfig.MemberSigningKey,
        Issuer = tokenConfig.Issuer,
        Audience = tokenConfig.Audience,
        DurationInHours = tokenConfig.AccessTokenLifetime,
    };

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

    private static MemberProfileResponse ToProfile(MemberEntity m) => new(
        m.Id, m.FirstName, m.LastName, m.Email,
        m.Phone, m.MemberNumber, m.GraduationYear,
        m.DepartmentId, m.Company, m.JobTitle,
        m.Location, m.LinkedInUrl, m.Bio,
        m.ProfilePictureUrl, m.Status, m.EmploymentStatus,
        m.IsMembershipActive, m.MembershipExpiry, m.MembershipYearsPaid, m.LastMembershipPaidAt);
}
