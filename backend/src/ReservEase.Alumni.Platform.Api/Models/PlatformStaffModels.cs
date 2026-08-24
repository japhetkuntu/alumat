using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

public class CreatePlatformStaffRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Support";
    public string? Team { get; set; }
}

public class UpdatePlatformStaffRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "Support";
    public string? Team { get; set; }
    public bool IsDisabled { get; set; }
}

public record PlatformStaffResponse(
    string Id, string Name, string Email, string Role, string? Team,
    bool Mfa, bool IsDisabled, DateTime? LastActiveAt);
