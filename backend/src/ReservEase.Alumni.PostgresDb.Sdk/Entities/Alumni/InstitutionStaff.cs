using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class InstitutionStaff : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public int? YearGroup { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
