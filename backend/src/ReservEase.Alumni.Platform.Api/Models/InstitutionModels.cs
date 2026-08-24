using System.ComponentModel.DataAnnotations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Models;

public class CreateInstitutionRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(63), RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug must be lowercase letters, numbers, and hyphens only")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public string ContactName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;

    public string Plan { get; set; } = "Starter";

    // Branding (from the onboarding wizard's Branding step)
    public string? PortalName { get; set; }
    public string? SupportEmail { get; set; }
    public string PrimaryColorHex { get; set; } = "#2563eb";

    // First admin (from the onboarding wizard's First Admin step)
    [Required]
    public string AdminFirstName { get; set; } = string.Empty;
    [Required]
    public string AdminLastName { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;
    [Required, MinLength(8)]
    public string AdminPassword { get; set; } = string.Empty;
}

public record InstitutionListItemResponse(
    string Id, string Name, string Slug, string? CustomDomain,
    string ContactName, string ContactEmail, string Plan, string Status,
    int MemberCount, int MemberLimit, DateTime OnboardedAt, decimal Mrr);

public record InstitutionDetailResponse(
    string Id, string Name, string Slug, string? CustomDomain,
    string PortalName, string? Tagline, string ContactName, string ContactEmail, string? SupportEmail,
    string? LogoUrl, string? IconUrl, string PrimaryColorHex,
    string? InstitutionPortalTitle, string? InstitutionAuthHeadline, string? InstitutionAuthSubtext,
    string? MemberPortalTitle, string? MemberAuthHeadline, string? MemberAuthSubtext,
    bool RequireStudentId, List<string> DisabledFeatures,
    List<LandingPageStory> LandingPageStories, NewsBanner? NewsBanner,
    string Plan, string Status, int MemberCount, int MemberLimit, int StorageUsedGb, int StorageLimitGb,
    DateTime OnboardedAt, DateTime? TrialEndsAt, decimal Mrr);

public class UpdateInstitutionStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty; // Trial, Active, Suspended, Cancelled
}

public class UpdateInstitutionPlanRequest
{
    [Required]
    public string Plan { get; set; } = string.Empty;
    public int? MemberLimit { get; set; }
    public int? StorageLimitGb { get; set; }
}

public class UpdateInstitutionBrandingRequest
{
    [Required, MaxLength(200)]
    public string PortalName { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    [Required, EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;
    [EmailAddress]
    public string? SupportEmail { get; set; }
    public string? LogoUrl { get; set; }
    public string? IconUrl { get; set; }
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string PrimaryColorHex { get; set; } = "#2563eb";

    // Per-portal content — all optional; a blank value falls back to the
    // platform's generic default copy (see each frontend app's theme.ts).
    [MaxLength(100)]
    public string? InstitutionPortalTitle { get; set; }
    [MaxLength(200)]
    public string? InstitutionAuthHeadline { get; set; }
    [MaxLength(500)]
    public string? InstitutionAuthSubtext { get; set; }
    [MaxLength(100)]
    public string? MemberPortalTitle { get; set; }
    [MaxLength(200)]
    public string? MemberAuthHeadline { get; set; }
    [MaxLength(500)]
    public string? MemberAuthSubtext { get; set; }

    public bool RequireStudentId { get; set; } = true;
}

public class UpdateInstitutionFeaturesRequest
{
    /// <summary>Feature keys to disable — see InstitutionFeatures for valid values. Any key not listed is enabled.</summary>
    public List<string> DisabledFeatures { get; set; } = [];
}

/// <summary>The one piece of content institution admins may also edit themselves — see Institution.Api's InstitutionController.UpdateLandingContent.</summary>
public class UpdateInstitutionLandingContentRequest
{
    public List<LandingPageStory> LandingPageStories { get; set; } = [];
    public NewsBanner? NewsBanner { get; set; }
}

public record SlugAvailabilityResponse(string Slug, bool Available);

public record PlatformDashboardSummary(
    int TotalInstitutions, int ActiveCount, int TrialCount,
    int TotalMembers, int NewInstitutionsThisMonth,
    decimal Mrr, List<int> GrowthLast6Months, List<string> GrowthMonthLabels);
