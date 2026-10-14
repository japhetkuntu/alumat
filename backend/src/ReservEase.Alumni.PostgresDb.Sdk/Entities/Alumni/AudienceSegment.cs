namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A named audience an administrator defines once and reuses ("Kumasi chapter, dues unpaid", "everyone who joined this
/// year"). The saved thing is the rule, not a list of people, so it always resolves against the members as they are now.
/// </summary>
public class AudienceSegment : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SegmentCriteria Criteria { get; set; } = new();
}

/// <summary>Every field narrows the audience; a field left empty does not narrow it.</summary>
public class SegmentCriteria
{
    /// <summary>Member status, e.g. "Active". Empty means any status that is not deleted.</summary>
    public string? Status { get; set; }

    /// <summary>Approved members of any of these groups.</summary>
    public List<string> GroupIds { get; set; } = [];

    /// <summary>Approved members of any group of one of these kinds (Chapter, Committee…).</summary>
    public List<string> GroupKinds { get; set; } = [];

    public int? GraduationYearFrom { get; set; }
    public int? GraduationYearTo { get; set; }
    public string? DepartmentId { get; set; }

    /// <summary>"Paid" or "Unpaid" for this year's membership dues. Empty means not considered.</summary>
    public string? DuesStatus { get; set; }

    /// <summary>Members whose answer to any of the community's own questions contains this text.</summary>
    public string? AnswerContains { get; set; }

    /// <summary>Only members who have not signed in for 60 days.</summary>
    public bool DormantOnly { get; set; }

    /// <summary>Only members who joined in the last this-many days.</summary>
    public int? JoinedWithinDays { get; set; }
}

public static class SegmentDuesStatuses
{
    public const string Paid = "Paid";
    public const string Unpaid = "Unpaid";
    public static bool IsValid(string? v) => string.IsNullOrEmpty(v) || v is Paid or Unpaid;
}
