namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ForumPost : BaseEntity
{
    public string ThreadId { get; set; } = string.Empty;
    public ForumThreadSnapshot? Thread { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public MemberSnapshot? Author { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
