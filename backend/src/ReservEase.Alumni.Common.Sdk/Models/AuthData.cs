namespace ReservEase.Alumni.Common.Sdk.Models;

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

    // Institution-staff scoping (only meaningful for a "ScopedAdmin" role) —
    // which year-groups/batches and communities this staff account is
    // restricted to. Unrelated to GraduationYear above, which is a member's
    // own graduation year, not an admin's assigned scope.
    public List<int>? YearGroups { get; set; }
    public List<string>? CommunityIds { get; set; }
}
