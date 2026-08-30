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
    public string? Message { get; set; }
}

public class UpdateOnboardingLeadStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty; // New, Contacted, Approved, Rejected
    public string? ApprovedInstitutionId { get; set; }
}

// AddInternalNoteRequest is shared with SupportCase's identical request model — see SupportModels.cs.

public record OnboardingLeadResponse(
    string Id, string InstitutionName, string ContactName, string ContactEmail, string? ContactPhone,
    string? Country, string? EstimatedMemberCount, string? Message, string Status,
    string? AssigneeStaffId, string? AssigneeName, string? InternalNote, string? ApprovedInstitutionId,
    double AgeHours);
