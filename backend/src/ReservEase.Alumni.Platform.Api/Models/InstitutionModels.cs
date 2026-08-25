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
    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string? SecondaryColorHex { get; set; }

    // Revenue & payment split (from the onboarding wizard's Payments & payouts step)
    [Range(0, 100)]
    public decimal PlatformFeePercentage { get; set; }
    public string? SettlementBankCode { get; set; }
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }

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
    int MemberCount, int MemberLimit, DateTime OnboardedAt, decimal Mrr,
    decimal PlatformFeePercentage, decimal Revenue,
    string MemberPortalUrl, string InstitutionPortalUrl);

public record InstitutionDetailResponse(
    string Id, string Name, string Slug, string? CustomDomain,
    string PortalName, string? Tagline, string ContactName, string ContactEmail, string? SupportEmail,
    string? LogoUrl, string? IconUrl, string PrimaryColorHex, string? SecondaryColorHex,
    string? InstitutionPortalTitle, string? InstitutionAuthHeadline, string? InstitutionAuthSubtext,
    string? MemberPortalTitle, string? MemberAuthHeadline, string? MemberAuthSubtext,
    bool RequireStudentId, List<string> DisabledFeatures,
    List<LandingPageStory> LandingPageStories, NewsBanner? NewsBanner,
    string Plan, string Status, int MemberCount, int MemberLimit, int StorageUsedGb, int StorageLimitGb,
    DateTime OnboardedAt, DateTime? TrialEndsAt, decimal Mrr,
    decimal PlatformFeePercentage, string? PaystackSubaccountCode,
    string? SettlementBankCode, string? SettlementBankName,
    string? SettlementAccountNumber, string? SettlementAccountName, decimal Revenue,
    string MemberPortalUrl, string InstitutionPortalUrl);

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
    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string? SecondaryColorHex { get; set; }

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

/// <summary>
/// The platform's cut of every online payment an institution collects, plus
/// the Paystack settlement banking details that back it — editable any time,
/// which re-syncs the institution's Paystack subaccount (created on first
/// save if it doesn't exist yet).
/// </summary>
public class UpdateInstitutionPaymentsRequest
{
    [Required, Range(0, 100)]
    public decimal PlatformFeePercentage { get; set; }
    [Required]
    public string SettlementBankCode { get; set; } = string.Empty;
    [Required]
    public string SettlementBankName { get; set; } = string.Empty;
    [Required]
    public string SettlementAccountNumber { get; set; } = string.Empty;
    [Required]
    public string SettlementAccountName { get; set; } = string.Empty;
}

/// <summary>How money moved for one institution — gross collected online, the platform's cut, and the institution's net.</summary>
public record InstitutionRevenueResponse(
    string InstitutionId, decimal GrossCollected, decimal PlatformFeeTotal,
    decimal NetToInstitution, int ConfirmedPaymentCount);

public record FeatureCatalogItem(string Key, string Label, string Description);

public record SlugAvailabilityResponse(string Slug, bool Available);

public record BaseDomainsResponse(string MemberBaseDomain, string AdminBaseDomain);

public record BankOption(string Name, string Code);

public record ResolvedAccountResponse(string AccountNumber, string AccountName);

public record PlatformDashboardSummary(
    int TotalInstitutions, int ActiveCount, int TrialCount,
    int TotalMembers, int NewInstitutionsThisMonth,
    decimal Mrr, decimal Revenue, List<int> GrowthLast6Months, List<string> GrowthMonthLabels);
