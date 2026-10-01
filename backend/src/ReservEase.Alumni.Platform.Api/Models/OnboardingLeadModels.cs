using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

public class CreateOnboardingLeadRequest
{
    [Required, MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;
    [Required]
    public string ContactName { get; set; } = string.Empty;
    [Required, EmailAddress]
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
}

public class UpdateOnboardingLeadStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty; // see OnboardingLeadStatuses
    public string? ApprovedInstitutionId { get; set; }
}

/// <summary>
/// A lead platform staff log themselves — outreach, a warm intro, a referral —
/// as opposed to one arriving through the public request form. Email is
/// optional here: early outreach often starts with only a phone number.
/// </summary>
public class CreateStaffOnboardingLeadRequest
{
    [Required, MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;
    [EmailAddress]
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactRole { get; set; }
    public string? OrganizationType { get; set; }
    public string? EstimatedMemberCount { get; set; }
    [Required, MaxLength(60)]
    public string Source { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Note { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
}

/// <summary>Edits a lead's details, owner and follow-up date. Every field is written as sent — null clears an optional field.</summary>
public class UpdateOnboardingLeadRequest
{
    [Required, MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;
    [EmailAddress]
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactRole { get; set; }
    public string? OrganizationType { get; set; }
    public string? EstimatedMemberCount { get; set; }
    [MaxLength(60)]
    public string? Source { get; set; }
    public string? AssigneeStaffId { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
}

public class ImportOnboardingLeadsRequest
{
    [Required, MinLength(1), MaxLength(1000)]
    public List<CreateStaffOnboardingLeadRequest> Rows { get; set; } = [];
}

public record ImportSkippedRow(int Row, string InstitutionName, string Reason);
public record ImportOnboardingLeadsResponse(int Created, List<ImportSkippedRow> Skipped);

public record LeadAssigneeResponse(string Id, string Name, string Role);

// AddInternalNoteRequest is shared with SupportCase's identical request model — see SupportModels.cs.

public record OnboardingLeadResponse(
    string Id, string InstitutionName, string ContactName, string ContactEmail, string? ContactPhone,
    string? Country, string? EstimatedMemberCount, string? OrganizationType, string? ContactRole,
    List<string> PrimaryGoals, string? CurrentMemberManagement, string? DataImportStatus,
    string? PreferredContactChannel, string? PreferredContactTime, string? TimeZone, string? Website,
    string? Message, string Status,
    string? AssigneeStaffId, string? AssigneeName, string? InternalNote, string? ApprovedInstitutionId,
    double AgeHours,
    string? AgreementVersion = null, DateTime? AgreementAcceptedAt = null, string? AgreementAcceptedByName = null,
    string? AgreementAcceptedByTitle = null, string? AgreementAcceptedIp = null,
    string? Source = null, DateTime? CreatedAt = null, DateTime? ContactedAt = null, DateTime? DemoBookedAt = null,
    DateTime? TrialStartedAt = null, DateTime? ApprovedAt = null,
    DateTime? NextFollowUpAt = null, DateTime? InstitutionTrialEndsAt = null, string? MarketingShareId = null, string? MarketingAttribution = null);
