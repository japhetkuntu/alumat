namespace ReservEase.Alumni.Operations.Worker.Models;

/// <summary>One member fan-out row, resolved in a single DB round trip so the workflow
/// doesn't need a second read per recipient to decide whether to also send SMS/WhatsApp.</summary>
public sealed class NotificationRecipient
{
    public required string MemberId { get; init; }
    public string? Phone { get; init; }
    public bool SmsAlerts { get; init; }
    public bool WhatsAppAlerts { get; init; }
}

/// <summary>The common single-recipient case: a member's contact fields bundled with
/// their NotificationPreference flags in one read.</summary>
public sealed class MemberWithPreference
{
    public required string MemberId { get; init; }
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? Phone { get; init; }
    public bool SmsAlerts { get; init; }
    public bool WhatsAppAlerts { get; init; }
    public bool EventReminders { get; init; } = true;
}

/// <summary>Just enough about the institution to build a portal URL, prefix an SMS with
/// its name, and check the SMS kill switch — mirrors both retired dispatchers'
/// GetInstitutionAsync/GetMemberPortalUrlAsync/GetAdminPortalUrlAsync helpers.</summary>
public sealed class InstitutionContactInfo
{
    public string? Name { get; init; }
    /// <summary>PortalName if set, else Name — the institution's own display name for its members, used
    /// wherever brand_name would otherwise fall back to the legal/registered Name.</summary>
    public string? PortalName { get; init; }
    public string? PrimaryColorHex { get; init; }
    public string? SecondaryColorHex { get; init; }
    public string? LogoUrl { get; init; }
    public bool SmsNotificationsEnabled { get; init; } = true;
    public bool EmailNotificationsEnabled { get; init; } = true;

    /// <summary>Fully-built "https://{slug}.{domain}" URLs, computed once in the activity
    /// (which alone may read IConfiguration — workflow code must stay deterministic).
    /// Empty when the institution or the relevant base-domain config is missing, same
    /// fallback both retired dispatchers used rather than ever pointing at a dev-only URL.</summary>
    public string MemberPortalUrl { get; init; } = string.Empty;
    public string AdminPortalUrl { get; init; } = string.Empty;
}

public sealed class JobAlertContent
{
    public required string Title { get; init; }
    public required string Company { get; init; }
    public required string Location { get; init; }
    public List<int>? YearGroups { get; init; }
}

public sealed class CampaignAlertContent
{
    public required string Title { get; init; }
    public List<int>? YearGroups { get; init; }
}

public sealed class EventReminderContent
{
    public required string Title { get; init; }
    public required DateTime StartDate { get; init; }
    public required string Venue { get; init; }
    public List<int>? YearGroups { get; init; }
}

public sealed class SpotlightAlertContent
{
    public required string Title { get; init; }
    public string? MemberFirstName { get; init; }
    public string? MemberLastName { get; init; }
}

public sealed class ClassNoteAlertContent
{
    public required string AuthorId { get; init; }
    public required int YearGroup { get; init; }
}
