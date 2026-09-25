using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Institution.Api.Models;

public record ContentReportDto(
    string Id, string EntityType, string EntityId, string EntityTitle, string Reason, string? Details,
    string ReporterName, string Status, DateTime CreatedAt,
    string? ReviewedByName, DateTime? ReviewedAt, string? ResolutionNote);

public class ReviewContentReportRequest
{
    /// <summary>ActionTaken or Dismissed (or Open, to reopen).</summary>
    [Required] public string Status { get; set; } = string.Empty;
    [MaxLength(500)] public string? Note { get; set; }
}
