using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

public class CreateSupportCaseRequest
{
    public string? InstitutionId { get; set; }
    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    [Required]
    public string Requester { get; set; } = string.Empty;
    [EmailAddress]
    public string? RequesterEmail { get; set; }
    [Required]
    public string Message { get; set; } = string.Empty;
}

public class UpdateSupportCaseStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty; // New, Investigating, Waiting on Internal Team, Resolved
}

public class AddInternalNoteRequest
{
    [Required]
    public string Note { get; set; } = string.Empty;
}

public record SupportCaseResponse(
    string Id, string Subject, string? InstitutionId, string? InstitutionName,
    string Severity, string Status, string? AssigneeStaffId, string? AssigneeName,
    double AgeHours, string Requester, string? RequesterEmail, string Message, string? InternalNote);
