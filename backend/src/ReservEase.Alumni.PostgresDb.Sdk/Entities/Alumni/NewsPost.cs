using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class NewsPost : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Null = institution-wide news post. Set = belongs to one Community; only its approved members/leaders can see it.</summary>
    public string? CommunityId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Published
    public DateTime? PublishedAt { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public MemberSnapshot? Author { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
    public List<int>? YearGroups { get; set; }
}
