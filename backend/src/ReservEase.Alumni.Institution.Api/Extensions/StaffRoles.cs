namespace ReservEase.Alumni.Institution.Api.Extensions;

/// <summary>
/// The three institution-staff access tiers. Centralized here so role
/// literals aren't duplicated (and drifting in case-sensitivity) across
/// every service that checks them.
/// </summary>
public static class StaffRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string ScopedAdmin = "ScopedAdmin";

    public static readonly string[] All = [SuperAdmin, Admin, ScopedAdmin];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
