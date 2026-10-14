using System.ComponentModel.DataAnnotations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Institution.Api.Models;

public record SegmentDto(string Id, string Name, string? Description, SegmentCriteria Criteria, DateTime CreatedAt);

public class SaveSegmentRequest
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public SegmentCriteria Criteria { get; set; } = new();
}

/// <summary>How many members a rule describes right now, and a few of them so the rule can be sanity-checked.</summary>
public record SegmentPreview(int Count, List<SegmentPreviewMember> Sample);
public record SegmentPreviewMember(string Id, string Name, string Email);
