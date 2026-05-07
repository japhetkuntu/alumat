namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ForumThread : BaseEntity
{
    public string CategoryId { get; set; } = string.Empty;
    public ForumCategorySnapshot? Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public MemberSnapshot? Author { get; set; }
    public bool IsPinned { get; set; }
    public bool IsClosed { get; set; }
    public int ReplyCount { get; set; }
}
