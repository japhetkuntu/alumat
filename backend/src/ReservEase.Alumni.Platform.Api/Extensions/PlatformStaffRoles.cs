namespace ReservEase.Alumni.Platform.Api.Extensions;

/// <summary>
/// The four platform-staff access tiers. Centralized here so role
/// literals aren't duplicated (and drifting in case-sensitivity) across
/// every service that checks them.
/// </summary>
public static class PlatformStaffRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Support = "Support";
    public const string Billing = "Billing";
    public const string Sales = "Sales";

    public static readonly string[] All = [SuperAdmin, Support, Billing, Sales];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
