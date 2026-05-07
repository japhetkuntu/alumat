namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ClassNote : BaseEntity
{
    public string AuthorId { get; set; } = string.Empty;
    public MemberSnapshot? Author { get; set; }
    public int YearGroup { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int LikeCount { get; set; }
    public bool IsDeleted { get; set; }
}
