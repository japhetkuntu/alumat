using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Member.Api.Models;

public class CreateContentReportRequest
{
    [Required] public string EntityType { get; set; } = string.Empty;
    [Required] public string EntityId { get; set; } = string.Empty;
    /// <summary>What the member saw, e.g. the thread title. Shown to admins beside the report; not trusted for anything else.</summary>
    [MaxLength(200)] public string? EntityTitle { get; set; }
    [Required, MaxLength(60)] public string Reason { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Details { get; set; }
}
