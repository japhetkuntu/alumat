namespace ReservEase.Alumni.Platform.Api.Models;

public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken, string AccessToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string Email, string NewPassword);

public record AuthUserResponse(string Id, string Email, string Name, string Role);
public record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresIn);
public record PlatformTokenResponse(AuthUserResponse User, AuthTokensResponse Tokens);

public class PlatformAuthClaimData
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int DurationInHours { get; set; } = 8;
}
