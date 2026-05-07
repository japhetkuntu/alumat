namespace Umat.Alumni.Member.Api.Models;

public class CachedRegistration
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? StudentId { get; set; }
    public int GraduationYear { get; set; }
    public string? DepartmentId { get; set; }
    public string Otp { get; set; } = string.Empty;
    public int ResendCount { get; set; }
    public string? ReferralCode { get; set; }
}
