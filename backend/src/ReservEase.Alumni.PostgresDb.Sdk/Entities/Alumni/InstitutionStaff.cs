using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class InstitutionStaff : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Optional. Needed to reach this admin by SMS — most existing staff won't have one until they add it.</summary>
    public string? Phone { get; set; }
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "SuperAdmin";

    // Only meaningful when Role == "ScopedAdmin" — Admin and SuperAdmin are
    // never restricted by these, regardless of whether they're populated.
    public List<int>? YearGroups { get; set; }
    public List<string>? CommunityIds { get; set; }

    public bool IsDisabled { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetSentAt { get; set; }
}
