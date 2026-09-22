namespace ReservEase.Alumni.Platform.Api.Models;

public record PlatformMemberListItem(
    string Id, string FirstName, string LastName, string Email,
    string InstitutionId, string InstitutionName,
    string OrganizationType,
    int GraduationYear, string Status,
    DateTime? LastLoginAt, bool IsActive, DateTime CreatedAt,
    string? ConnectionType, List<string>? Skills, List<string>? Interests,
    bool ShowEmailOnDirectory, bool ShowPhoneOnDirectory, bool ShowCompanyOnDirectory, bool ShowBioOnDirectory);

public class PlatformMemberFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public string? InstitutionId { get; set; }
    public string? Status { get; set; }
    /// <summary>true = only members who logged in within the active window; false = only members who haven't; null = everyone.</summary>
    public bool? ActiveOnly { get; set; }
}

public class UpdatePlatformMemberProfileRequest
{
    public string? ConnectionType { get; set; }
    public List<string>? Skills { get; set; }
    public List<string>? Interests { get; set; }
    public bool? ShowEmailOnDirectory { get; set; }
    public bool? ShowPhoneOnDirectory { get; set; }
    public bool? ShowCompanyOnDirectory { get; set; }
    public bool? ShowBioOnDirectory { get; set; }
}
