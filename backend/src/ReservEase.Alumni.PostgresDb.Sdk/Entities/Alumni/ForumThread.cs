using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ForumThread : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;
    public ForumCategorySnapshot? Category { get; set; }

    /// <summary>Null = institution-wide thread (unchanged, pre-existing behavior). Set = belongs to one Community; only its approved members/leaders can see or reply to it.</summary>
    public string? CommunityId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public MemberSnapshot? Author { get; set; }
    public bool IsPinned { get; set; }
    public bool IsClosed { get; set; }
    public int ReplyCount { get; set; }
}
