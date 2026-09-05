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
}

public record OnboardingLeadResponse(string Id, string InstitutionName, string ContactEmail, string Status);
