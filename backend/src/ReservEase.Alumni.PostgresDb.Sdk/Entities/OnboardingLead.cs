namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A prospective institution's request to be onboarded. Platform-level, pre-tenant, not tenant-scoped.</summary>
public class OnboardingLead : BaseEntity
{
    public string InstitutionName { get; set; } = string.Empty;

    // Electronic acceptance of the Institution Agreement, recorded when the request was submitted.
    public string? AgreementVersion { get; set; }
    public DateTime? AgreementAcceptedAt { get; set; }
    public string? AgreementAcceptedByName { get; set; }
    public string? AgreementAcceptedByTitle { get; set; }
    public string? AgreementAcceptedIp { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? Country { get; set; }
    public string? EstimatedMemberCount { get; set; }
    public string? OrganizationType { get; set; }
    public string? ContactRole { get; set; }
    public List<string> PrimaryGoals { get; set; } = [];
    public string? CurrentMemberManagement { get; set; }
    public string? DataImportStatus { get; set; }
    public string? PreferredContactChannel { get; set; }
    public string? PreferredContactTime { get; set; }
    public string? TimeZone { get; set; }
    public string? Website { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "New"; // see OnboardingLeadStatuses
    /// <summary>Where the lead came from — "Website" for the public request form, otherwise whatever platform staff chose when logging it (e.g. "Warm intro", "Outreach", "Referral").</summary>
    public string? Source { get; set; }
    public string? MarketingShareId { get; set; }
    public string? MarketingAttribution { get; set; }
    // When each funnel stage was first reached — set once, never cleared, so the
    // platform's onboarding funnel can count "reached this stage in week N" even
    // after a lead moves on or is rejected. Skipping a stage backfills it.
    public DateTime? ContactedAt { get; set; }
    public DateTime? DemoBookedAt { get; set; }
    public DateTime? TrialStartedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    /// <summary>When the assignee should next get back to this lead. The Operations Worker reminds them once it's due.</summary>
    public DateTime? NextFollowUpAt { get; set; }
    /// <summary>When the worker last reminded the assignee — a reminder is due again only once NextFollowUpAt moves past it.</summary>
    public DateTime? FollowUpReminderSentAt { get; set; }
    public string? AssigneeStaffId { get; set; }
    public string? InternalNote { get; set; }
    public string? ApprovedInstitutionId { get; set; }
}
