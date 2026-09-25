namespace ReservEase.Alumni.Platform.Api.Models;

public record PlatformMemberListItem(
    string Id, string FirstName, string LastName, string Email,
    string InstitutionId, string InstitutionName,
    string OrganizationType,
    int GraduationYear, string Status,
    DateTime? LastLoginAt, bool IsActive, DateTime CreatedAt);

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
