using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Models;

public record AdminListItem(string Id, string FirstName, string LastName, string Email, string Role, int? GraduationYear, bool IsDisabled, DateTime CreatedAt);

public class CreateAdminRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public int? GraduationYear { get; set; }
    public bool IsDisabled { get; set; }
}

public class UpdateAdminRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public int? GraduationYear { get; set; }
    public bool IsDisabled { get; set; }
}

public class AdminFilter : BaseFilter
{
    public string? Role { get; set; }
    public int? GraduationYear { get; set; }
}
