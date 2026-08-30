namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A prospective institution's request to be onboarded. Platform-level, pre-tenant, not tenant-scoped.</summary>
public class OnboardingLead : BaseEntity
{
    public string InstitutionName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? Country { get; set; }
    public string? EstimatedMemberCount { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "New"; // New, Contacted, Approved, Rejected
    public string? AssigneeStaffId { get; set; }
    public string? InternalNote { get; set; }
    public string? ApprovedInstitutionId { get; set; }
}
