namespace ReservEase.Alumni.Institution.Api.Models;

public record LoginRequest(string Email, string Password);
/// <summary>ID token minted client-side by Google Identity Services, verified server-side before any staff lookup (see IGoogleTokenVerifier).</summary>
public record GoogleLoginRequest(string IdToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string Email, string NewPassword);
