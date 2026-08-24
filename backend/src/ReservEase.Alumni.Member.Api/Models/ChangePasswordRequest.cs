namespace ReservEase.Alumni.Member.Api.Models;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
