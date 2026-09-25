using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>
/// Submitted from the public marketing site's "onboard your institution"
/// form (see platform-marketing-page.tsx). Written directly to the shared
/// OnboardingLeads table — Member.Api never calls Platform.Api over the
/// network for this; platform staff review the same rows from their own
/// portal (Platform.Api's OnboardingLeadsController), since both APIs share
/// one AlumniDbContext/database.
/// </summary>
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
    public string? Message { get; set; }
    public string? OrganizationType { get; set; }
    public string? Website { get; set; }
    public List<string>? PrimaryGoals { get; set; }
    public string? CurrentMemberManagement { get; set; }
    public string? DataImportStatus { get; set; }
    public string? PreferredContactChannel { get; set; }
    public string? PreferredContactTime { get; set; }
    public string? TimeZone { get; set; }
    /// <summary>Must be true, and match the current agreement version.</summary>
    public bool AgreementAccepted { get; set; }
    public string? AgreementVersion { get; set; }
    /// <summary>The role the person gave, e.g. "Chairperson".</summary>
    public string? ContactRole { get; set; }
}

public record OnboardingLeadResponse(string Id, string InstitutionName, string ContactEmail, string Status);
