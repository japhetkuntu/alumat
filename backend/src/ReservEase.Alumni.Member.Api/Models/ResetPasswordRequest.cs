namespace ReservEase.Alumni.Member.Api.Models;

public record ResetPasswordRequest(string Token, string Email, string NewPassword);
