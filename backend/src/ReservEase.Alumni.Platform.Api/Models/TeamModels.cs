using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>One person in the platform team, with how their own work is going. Counts are their own, not their team's.</summary>
public record TeamMemberDto(
    string Id, string Name, string Email, string Role, string? ReportsToStaffId, bool IsDisabled, DateTime? LastActiveAt,
    int DirectReports, int Institutions, int ActiveInstitutions, int Members, int OpenLeads, int ConvertedLeads);

public record TeamViewDto(
    string MyId, string MyRole, bool CanManage,
    List<TeamMemberDto> People,
    // Only SuperAdmins see these: work nobody has been given yet.
    int? UnassignedInstitutions, int? UnassignedLeads);

public class SetManagerRequest
{
    /// <summary>Null is only valid for a SuperAdmin.</summary>
    public string? ManagerId { get; set; }
}

public class ReassignRequest
{
    [Required]
    public string ToStaffId { get; set; } = string.Empty;
    public List<string> InstitutionIds { get; set; } = [];
    public List<string> LeadIds { get; set; } = [];
}

public record ReassignResult(int Institutions, int Leads);

public record TeamAssigneeDto(string Id, string Name, string Role);
