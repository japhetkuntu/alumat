namespace Umat.Alumni.Common.Sdk.Models;

public class AuthData
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public string Name => $"{FirstName} {LastName}".Trim();
    public string Role { get; set; } = string.Empty;
    public int? GraduationYear { get; set; }
    public string MobileNumber { get; set; } = string.Empty;
}
