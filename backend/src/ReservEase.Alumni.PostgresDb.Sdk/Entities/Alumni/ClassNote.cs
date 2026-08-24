using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ClassNote : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;
    public MemberSnapshot? Author { get; set; }
    public int YearGroup { get; set; }

    /// <summary>Null = posted to the institution-wide year-group wall (unchanged, pre-existing behavior). Set = posted to one Community's wall instead — visible to its approved members/leaders regardless of graduation year.</summary>
    public string? CommunityId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int LikeCount { get; set; }
    public bool IsDeleted { get; set; }
}
