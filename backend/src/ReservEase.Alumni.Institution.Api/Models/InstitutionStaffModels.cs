using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Models;

public record InstitutionStaffListItem(string Id, string FirstName, string LastName, string Email, string Role, int? GraduationYear, bool IsDisabled, DateTime CreatedAt);

public class CreateInstitutionStaffRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public int? GraduationYear { get; set; }
    public bool IsDisabled { get; set; }
}

public class UpdateInstitutionStaffRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public int? GraduationYear { get; set; }
    public bool IsDisabled { get; set; }
}

public class InstitutionStaffFilter : BaseFilter
{
    public string? Role { get; set; }
    public int? GraduationYear { get; set; }
}
