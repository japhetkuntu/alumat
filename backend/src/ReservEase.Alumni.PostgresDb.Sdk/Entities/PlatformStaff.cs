namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A SaaS-operator user — manages institutions, plans, and billing. Not tenant-scoped.</summary>
public class PlatformStaff : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Support"; // SuperAdmin, Support, Billing, Sales
    public string? Team { get; set; }
    public bool Mfa { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime? LastActiveAt { get; set; }
}
