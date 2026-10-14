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

    /// <summary>
    /// Onboards institutions and helps them grow. Reports to a SuperAdmin or another Representative, sees only the
    /// institutions and leads owned by themselves and the people under them, and can never approve settlement details.
    /// </summary>
    public const string Representative = "Representative";

    public static readonly string[] All = [SuperAdmin, Representative, Support, Billing, Sales];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
