namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A member's report about something another member posted (a forum thread, a mentor profile, a job, a business
/// listing or a spotlight). The institution's admins review it; AlumUnion only provides the tool.
/// </summary>
public class ContentReport : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string ReporterMemberId { get; set; } = string.Empty;
    /// <summary>Copied at report time so the report still reads correctly if the member is later removed.</summary>
    public string ReporterName { get; set; } = string.Empty;

    /// <summary>One of <see cref="ContentReportTypes"/>.</summary>
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    /// <summary>Title as it read when reported, so the admin sees what was reported even if it is edited or deleted.</summary>
    public string EntityTitle { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }

    /// <summary>One of <see cref="ContentReportStatuses"/>.</summary>
    public string Status { get; set; } = ContentReportStatuses.Open;
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ResolutionNote { get; set; }
}

public static class ContentReportTypes
{
    public const string ForumThread = "ForumThread";
    public const string MentorProfile = "MentorProfile";
    public const string Job = "Job";
    public const string BusinessListing = "BusinessListing";
    public const string Spotlight = "Spotlight";

    public static readonly string[] All = [ForumThread, MentorProfile, Job, BusinessListing, Spotlight];
}

public static class ContentReportStatuses
{
    public const string Open = "Open";
    public const string ActionTaken = "ActionTaken";
    public const string Dismissed = "Dismissed";

    public static readonly string[] All = [Open, ActionTaken, Dismissed];
}

public static class ContentReportReasons
{
    public static readonly string[] All =
    [
        "Scam or fraud",
        "Harassment or abuse",
        "False or misleading",
        "Inappropriate content",
        "Other",
    ];
}
