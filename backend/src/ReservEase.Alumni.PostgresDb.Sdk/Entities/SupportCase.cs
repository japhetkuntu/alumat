namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A support ticket raised by (or on behalf of) an institution. Global, not tenant-scoped.</summary>
public class SupportCase : BaseEntity
{
    public string? InstitutionId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // High, Medium, Low
    public string Status { get; set; } = "New"; // New, Investigating, Waiting on Internal Team, Resolved
    public string? AssigneeStaffId { get; set; }
    public string Requester { get; set; } = string.Empty;
    public string? RequesterEmail { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? InternalNote { get; set; }
}
