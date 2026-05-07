namespace Umat.Alumni.Admin.Api.Models;

public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken, string AccessToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ResetPasswordRequest(string Token, string Email, string NewPassword);
