namespace ReservEase.Alumni.Institution.Api.Models;

public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken, string AccessToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string Email, string NewPassword);
